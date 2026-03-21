namespace Fusion.Addons.KCC
{
	using System;
	using System.Collections.Generic;
	using UnityEngine;
	using Fusion;

	// All platform related objecs must respect this execution order to work correctly:
	// 1. Update of all IPlatform         => Calculating new position/rotation values and updating Transform and Rigidbody components.
	// 2. Update of all PlatformProcessor => IPlatform tracking, propagation of their Transform changes since last update to KCC Transform and KCCData.
	// 3. Update of all KCC               => Predicted movement and interpolation.

	/// <summary>
	/// Use this interface to mark a processor as platform.
	/// Make sure the script that moves with the platform object has lower execution order => it must be executed before <c>PlatformProcessor</c>.
	/// </summary>
	public interface IPlatform
	{
		NetworkObject Object { get; }
	}

	/// <summary>
	/// Interface to notify other processors about KCC being transformed.
	/// </summary>
	public interface IPlatformListener
	{
		void OnTransform(KCC kcc, KCCData data, Vector3 positionDelta, Quaternion rotationDelta);
	}

	/// <summary>
	/// This processor tracks overlapping platforms (KCC processors implementing <c>IPlatform</c>) and propagates their position and rotation changes to <c>KCC</c>.
	/// Make sure the script that moves with the <c>IPlatform</c> object has lower execution order => it must be executed before <c>PlatformProcessor</c> and <c>PlatformProcessorUpdater</c>.
	/// When <c>PlatformProcessor</c> propagates all platform changes, it notifies <c>IPlatformListener</c> processors with absolute transform deltas.
	/// </summary>
	[DefaultExecutionOrder(PlatformProcessor.EXECUTION_ORDER)]
    public unsafe class PlatformProcessor : NetworkKCCProcessor, IKCCProcessor, IBeginMove, IEndMove
    {
		// CONSTANTS

		public const int EXECUTION_ORDER = -400;
		public const int MAX_PLATFORMS   = 3;

		// PRIVATE MEMBERS

		[SerializeField][Tooltip("How long it takes to move the KCC from world space to platform space.")]
		private float _platformSpaceTransitionDuration = 0.5f;
		[SerializeField][Tooltip("How long it takes to move the KCC from platform space to world space.")]
		private float _worldSpaceTransitionDuration = 0.5f;
		[SerializeField][Tooltip("Controls how fast the KCC proxy moves locally towards the platform before it moves in fully interpolated platform space.")]
		private float _proxyPlatformEnterTime = 0.25f;

		[Networked]
		private ref ProcessorState _state => ref MakeRef<ProcessorState>();

		private KCC                 _kcc;
		private bool                _isSpawned;
		private Platform[]          _renderPlatforms    = new Platform[MAX_PLATFORMS];
		private PlatformTransform[] _platformTransforms = new PlatformTransform[MAX_PLATFORMS];
		private List<PlatformEnter> _platformEnters     = new List<PlatformEnter>();
		private List<PlatformExit>  _platformExits      = new List<PlatformExit>();

		private static List<IPlatform> _cachedPlatforms   = new List<IPlatform>();
		private static List<NetworkId> _cachedNetworkIds1 = new List<NetworkId>();
		private static List<NetworkId> _cachedNetworkIds2 = new List<NetworkId>();

		// PUBLIC METHODS

		/// <summary>
		/// Returns <c>true</c> if there is at least one platform tracked.
		/// </summary>
		public bool IsActive()
		{
			return _isSpawned == true && _state.HasPlatforms == true;
		}

		/// <summary>
		/// Called by <c>PlatformProcessorUpdater</c>. Do not use from user code.
		/// </summary>
		public void ProcessFixedUpdate()
		{
			if (ReferenceEquals(_kcc, null) == true)
				return;

			if (Object.IsInSimulation != _kcc.Object.IsInSimulation)
			{
				// Synchronize simulation state of the processor with KCC.
				Runner.SetIsSimulated(Object, _kcc.Object.IsInSimulation);
			}

			if (Object.IsInSimulation == true)
			{
				// Update state of platforms, track new, cleanup old.
				UpdatePlatforms(_kcc);

				if (_state.HasPlatforms == true)
				{
					// For predicted KCC, propagate position and rotation deltas of all platforms since last fixed update.
					PropagatePlatformMovement(_kcc, _kcc.FixedData, true);

					// Copy fixed state to render state as a base.
					SynchronizeRenderPlatforms();
				}
			}
			else
			{
				// Otherwise snap the KCC to tracked platforms based on interpolated offsets.
				// Notice we modify only position, this is essential to get correct results from KCC physics queries. Rotation keeps unchanged.
				InterpolateKCC(_kcc, _kcc.FixedData, true);
			}
		}

		/// <summary>
		/// Called by <c>PlatformProcessorUpdater</c>. Do not use from user code.
		/// </summary>
		public void ProcessRender()
		{
			if (ReferenceEquals(_kcc, null) == true)
				return;

			if (_kcc.IsPredictingInRenderUpdate == true)
			{
				if (_state.HasPlatforms == true)
				{
					// For render-predicted KCC, propagate position and rotation deltas of all platforms since last fixed or render update.
					PropagatePlatformMovement(_kcc, _kcc.RenderData, false);
				}
			}
			else
			{
				// Otherwise snap the KCC to tracked platforms based on interpolated offsets.
				// Notice we modify only position, this is essential to get correct results from KCC physics queries. Rotation keeps unchanged.
				InterpolateKCC(_kcc, _kcc.RenderData, true);
			}
		}

		// PlatformProcessor INTERFACE

		protected virtual void OnSpawned()                                      {}
		protected virtual void OnDespawned(NetworkRunner runner, bool hasState) {}

		// NetworkBehaviour INTERFACE

		public override sealed void Spawned()
		{
			_isSpawned = true;

			Runner.GetSingleton<PlatformProcessorUpdater>().Register(this);

			OnSpawned();
		}

		public override sealed void Despawned(NetworkRunner runner, bool hasState)
		{
			OnDespawned(runner, hasState);

			runner.GetSingleton<PlatformProcessorUpdater>().Unregister(this);

			_platformEnters.Clear();
			_platformExits.Clear();

			_kcc       = default;
			_isSpawned = default;
		}

		// NetworkKCCProcessor INTERFACE

		public override float GetPriority(KCC kcc) => float.MinValue;

		public override void OnEnter(KCC kcc, KCCData data)
		{
			_kcc = kcc;
		}

		public override void OnExit(KCC kcc, KCCData data)
		{
			_kcc = null;
		}

		public override void OnInterpolate(KCC kcc, KCCData data)
		{
			// This code path can be executed for:
			// 1. Proxy interpolated in fixed update.
			// 2. Proxy interpolated in render update.
			// 3. Input/State authority interpolated in render update.

			// For KCC proxy, KCCData.TargetPosition equals to snapshot interpolated position at this point.
			// However platforms are predicted everywhere - on all server and clients.
			// If a platform is predicted and KCC proxy interpolated in world space, it results in KCC proxy visual being delayed behind the predicted platform visual.

			// Following code interpolates KCC position in platform space(s), matching position of the platform visual.
			// [KCC position] = [local IPlatform position] + [interpolated IPlatform => KCC offset].

			InterpolateKCC(kcc, data, false);
		}

		// IKCCProcessor INTERFACE

		bool IKCCProcessor.IsActive(KCC kcc) => _isSpawned == true;

		// IBeginMove INTERFACE

		float IKCCStage<BeginMove>.GetPriority(KCC kcc) => float.MaxValue;

		void IKCCStage<BeginMove>.Execute(BeginMove stage, KCC kcc, KCCData data)
		{
			if (_state.HasPlatforms == false)
				return;

			// Disable prediction correction and anti-jitter if there is at least one platform tracked.
			// This must be called in both fixed and render update.
			kcc.SuppressFeature(EKCCFeature.PredictionCorrection);
			kcc.SuppressFeature(EKCCFeature.AntiJitter);
		}

		// IEndMove INTERFACE

		float IKCCStage<EndMove>.GetPriority(KCC kcc) => float.MaxValue;

		void IKCCStage<EndMove>.Execute(EndMove stage, KCC kcc, KCCData data)
		{
			if (_state.HasPlatforms == false)
				return;

			bool isInFixedUpdate = kcc.IsInFixedUpdate;

			// Update Platform => KCC offset after KCC moves.
			for (int i = 0; i < MAX_PLATFORMS; ++i)
			{
				Platform platform = GetPlatform(i, isInFixedUpdate);
				if (platform.State != EPlatformState.None)
				{
					platform.KCCOffset   = Quaternion.Inverse(platform.Rotation) * (data.TargetPosition - platform.Position);
					platform.KCCVelocity = data.RealVelocity;

					SetPlatform(i, platform, isInFixedUpdate);
				}
			}
		}

		// PRIVATE METHODS

		private void UpdatePlatforms(KCC kcc)
		{
			// 1. Get all platform objects tracked by KCC.
			kcc.GetProcessors<IPlatform>(_cachedPlatforms);

			// Early exit - performance optimziation.
			if (_cachedPlatforms.Count <= 0 && _state.HasPlatforms == false)
				return;

			_cachedNetworkIds1.Clear(); // Used to store platforms tracked by KCC.
			_cachedNetworkIds2.Clear(); // Used to store platforms tracked by PlatformProcessor.

			foreach (IPlatform platform in _cachedPlatforms)
			{
				_cachedNetworkIds1.Add(platform.Object.Id);
			}

			int activePlatforms = 0;

			// 2. Mark all platforms in PlatformProcessor state as inactive if they are not tracked by KCC.
			for (int i = 0; i < MAX_PLATFORMS; ++i)
			{
				Platform platform = _state.Platforms.Get(i);
				if (platform.State == EPlatformState.Active)
				{
					if (_cachedNetworkIds1.Contains(platform.Id) == true)
					{
						++activePlatforms;
					}
					else
					{
						platform.State = EPlatformState.Inactive;
						_state.Platforms.Set(i, platform);
					}
				}

				if (platform.Id.IsValid == true)
				{
					_cachedNetworkIds2.Add(platform.Id);
				}
			}

			// 3. Register all platforms tracked by KCC that are not tracked by PlatformProcessor.
			foreach (IPlatform trackedPlatform in _cachedPlatforms)
			{
				NetworkId platformId = trackedPlatform.Object.Id;
				if (platformId.IsValid == true && _cachedNetworkIds2.Contains(platformId) == false)
				{
					// The platform is not yet tracked by PlatformProcessor. Let's try adding it.
					for (int i = 0; i < MAX_PLATFORMS; ++i)
					{
						if (_state.Platforms.Get(i).State == EPlatformState.None)
						{
							trackedPlatform.Object.transform.GetPositionAndRotation(out Vector3 platformPosition, out Quaternion platformRotation);

							Platform platform = new Platform();
							platform.Id          = platformId;
							platform.State       = EPlatformState.Active;
							platform.Alpha       = 0.0f;
							platform.Position    = platformPosition;
							platform.Rotation    = platformRotation;
							platform.KCCOffset   = Quaternion.Inverse(platformRotation) * (kcc.FixedData.TargetPosition - platformPosition);
							platform.KCCVelocity = kcc.FixedData.RealVelocity;

							_state.Platforms.Set(i, platform);
							_cachedNetworkIds2.Add(platformId);

							++activePlatforms;

							break;
						}
					}
				}
			}

			bool hasPlatforms = false;

			// If there is another platform active, the transition is between two different platform spaces (world space not involved).
			float inactiveTransitionDuration = activePlatforms > 0 ? _platformSpaceTransitionDuration : _worldSpaceTransitionDuration;

			// 4. Update active and inactive platforms alpha values.
			// The platform alpha defines how much is the KCC position affected by the platform and is used for smooth transition from world space to platform space.
			for (int i = 0; i < MAX_PLATFORMS; ++i)
			{
				Platform platform = _state.Platforms.Get(i);
				if (platform.State == EPlatformState.Active)
				{
					hasPlatforms = true;

					if (platform.Alpha < 1.0f)
					{
						// The KCC stands within the platform, increasing alpha to 1.0f.
						platform.Alpha = _platformSpaceTransitionDuration > 0.001f ? Mathf.Min(platform.Alpha + Runner.DeltaTime / _platformSpaceTransitionDuration, 1.0f) : 1.0f;
						_state.Platforms.Set(i, platform);
					}
				}
				else if (platform.State == EPlatformState.Inactive)
				{
					// The KCC left the the platform, decreasing alpha to 0.0f.
					platform.Alpha -= inactiveTransitionDuration > 0.001f ? (Runner.DeltaTime / inactiveTransitionDuration) : 1.0f;

					if (platform.Alpha <= 0.0f)
					{
						// Once the alpha is 0.0f, we can remove the platform entirely.
						platform = default;
					}
					else
					{
						hasPlatforms = true;
					}

					_state.Platforms.Set(i, platform);
				}
			}

			_state.HasPlatforms = hasPlatforms;
		}

		private void PropagatePlatformMovement(KCC kcc, KCCData data, bool isInFixedUpdate)
		{
			bool       synchronize      = false;
			Vector3    basePosition     = data.TargetPosition;
			Quaternion baseRotation     = data.TransformRotation;
			Vector3    minPositionDelta = default;
			Vector3    maxPositionDelta = default;

			// 1. Iterate over all tracked platforms and calculate their position and rotation deltas.
			for (int i = 0; i < MAX_PLATFORMS; ++i)
			{
				Platform platform = GetPlatform(i, isInFixedUpdate);

				// Propagate movement only for platforms marked as active.
				// Inactive platforms (marked immediately after losing overlap) don't propagate their movement, but they are still tracked.
				if (platform.State != EPlatformState.Active)
					continue;

				NetworkObject platformObject = Runner.FindObject(platform.Id);
				if (ReferenceEquals(platformObject, null) == true || platformObject.TryGetComponent(out IPlatform synchronizePlatform) == false)
					continue;

				platformObject.transform.GetPositionAndRotation(out Vector3 currentPlatformPosition, out Quaternion currentPlatformRotation);

				// Calculate platform position and rotation delta since last update.
				Vector3    platformPositionDelta = currentPlatformPosition - platform.Position;
				Quaternion platformRotationDelta = Quaternion.Inverse(platform.Rotation) * currentPlatformRotation;

				// The platform rotated, we have to rotate stored KCC position offset.
				Vector3 recalculatedKCCOffset   = platformRotationDelta * platform.KCCOffset;
				Vector3 recalculatedKCCVelocity = platformRotationDelta * platform.KCCVelocity;

				// Calculate delta between old and new KCC position offset. This needs to be added to KCC to stay on a platform spot.
				Vector3 kccOffsetDelta = recalculatedKCCOffset - platform.KCCOffset;

				// Final KCC position delta is calculated as sum of platform delta and KCC offset delta.
				// Notice the KCC offset is in platform local space so it needs to be rotated.
				Vector3 kccPositionDelta = platformPositionDelta + currentPlatformRotation * kccOffsetDelta;

				// Find min and max position delta from all platforms.
				// This ensures there's only highest delta applied when crossing between platforms.
				minPositionDelta = Vector3.Min(minPositionDelta, kccPositionDelta);
				maxPositionDelta = Vector3.Max(maxPositionDelta, kccPositionDelta);

				// Propagate rotation delta to the KCC.
				data.AddLookRotation(0.0f, platformRotationDelta.eulerAngles.y);

				// Update platform properties with new values.
				platform.Position    = currentPlatformPosition;
				platform.Rotation    = currentPlatformRotation;
				platform.KCCOffset   = recalculatedKCCOffset;
				platform.KCCVelocity = recalculatedKCCVelocity;

				// Update PlatformProcessor state.
				SetPlatform(i, platform, isInFixedUpdate);

				// Set flag to synchronize Transform and Ridigbody components.
				synchronize = true;
			}

			// 2. Calculate and propagate final position delta to the KCC. Rotation delta is already propagated.
			Vector3 finalPositionDelta = minPositionDelta + maxPositionDelta;
			data.BasePosition    += finalPositionDelta;
			data.DesiredPosition += finalPositionDelta;
			data.TargetPosition  += finalPositionDelta;

			// 3. Deltas from all platforms are propagated, now we have to recalculate Platform => KCC offsets.
			for (int i = 0; i < MAX_PLATFORMS; ++i)
			{
				Platform platform = GetPlatform(i, isInFixedUpdate);
				if (platform.State == EPlatformState.None)
					continue;

				// Offset needs to be calculated for both Active and Inactive platforms.
				platform.KCCOffset = Quaternion.Inverse(platform.Rotation) * (data.TargetPosition - platform.Position);

				// Update PlatformProcessor state.
				SetPlatform(i, platform, isInFixedUpdate);
			}

			if (synchronize == true)
			{
				// There is at least one platform tracked, Transform and Rigidbody should be refreshed before any KCC begins predicted move.
				kcc.SynchronizeTransform(true, true, false);

				Vector3    positionDelta = data.TargetPosition - basePosition;
				Quaternion rotationDelta = Quaternion.Inverse(baseRotation) * data.TransformRotation;

				// Notify all listeners.
				foreach (IPlatformListener listener in kcc.GetProcessors<IPlatformListener>(true))
				{
					try
					{
						listener.OnTransform(kcc, data, positionDelta, rotationDelta);
					}
					catch (Exception exception)
					{
						Debug.LogException(exception);
					}
				}
			}
		}

		private void InterpolateKCC(KCC kcc, KCCData data, bool synchronizeTransform)
		{
			// At this point all platforms (IPlatform) should have updated their transforms.
			// This method calculates interpolated position of the KCC by taking local platform positions + interpolated Position => KCC offsets.
			// Calculations below result in smooth transition between world and multiple platform spaces.

			bool buffersValid = TryGetSnapshotsBuffers(out NetworkBehaviourBuffer fromBuffer, out NetworkBehaviourBuffer toBuffer, out float alpha);
			if (buffersValid == false)
				return;

			ProcessorState fromState = fromBuffer.ReinterpretState<ProcessorState>();
			ProcessorState toState   = toBuffer.ReinterpretState<ProcessorState>();

			Vector3 worldSpacePosition = data.TargetPosition;
			Vector3 worldSpaceVelocity = data.RealVelocity;

			if (kcc.GetInterpolatedNetworkBufferPosition(out Vector3 interpolatedKCCPosition) == true)
			{
				worldSpacePosition = interpolatedKCCPosition;
			}

			CachePlatformTransforms();

			ProcessPlatformEnters(fromState, toState, alpha);
			ProcessPlatformExits(fromState, toState, alpha);

			GetPlatformSpacePositionAndVelocity(worldSpacePosition, worldSpaceVelocity, fromState, out Vector3 fromPlatformSpacePosition, out Vector3 fromPlatformSpaceVelocity);
			GetPlatformSpacePositionAndVelocity(worldSpacePosition, worldSpaceVelocity, toState,   out Vector3 toPlatformSpacePosition,   out Vector3 toPlatformSpaceVelocity);

			data.TargetPosition = Vector3.Lerp(fromPlatformSpacePosition, toPlatformSpacePosition, alpha);

			if (kcc.Object.IsInSimulation == false)
			{
				// For proxies the KCC velocity is calculated locally from Transform changes.
				// This involves also platform movement which results in non-zero velocity while standing still. This is undesired.
				data.RealVelocity = Vector3.Lerp(fromPlatformSpaceVelocity, toPlatformSpaceVelocity, alpha);
				data.RealSpeed    = Vector3.Magnitude(data.RealVelocity);
			}

			// From latest [Networked] state snapshot we know that the interpolated KCC will enter a platform.
			// We can early interpolate towards the platform before the interpolated enter kicks in.
			ApplyPreEnterOffset(data);

			if (synchronizeTransform == true)
			{
				kcc.SynchronizeTransform(true, false, false);
			}
		}

		private void ProcessPlatformEnters(ProcessorState fromState, ProcessorState toState, float alpha)
		{
			// 1. Decrease impact of the local offset (controlled by PlatformEnter.Alpha). When the platform is fully Active, the impact of local offset is zero.
			//    After leaving a platform (None/Inactive), the impact goes to zero as well. At Alpha = 0.0f the PlatformEnter instance will be removed.
			for (int i = _platformEnters.Count - 1; i >= 0; --i)
			{
				PlatformEnter platformEnter = _platformEnters[i];

				if (_state.TryGetPlatform(platformEnter.Id, out Platform platform) == true && platform.State == EPlatformState.Active)
				{
					if (platformEnter.StateAlpha > 0.0f && toState.TryGetPlatform(platformEnter.Id, out Platform toPlatform) == true)
					{
						float fromPlatformAlpha = fromState.TryGetPlatform(platformEnter.Id, out Platform fromPlatform) == true ? fromPlatform.Alpha : 0.0f;
						platformEnter.StateAlpha = Mathf.Min(platformEnter.StateAlpha, 1.0f - Mathf.Lerp(fromPlatformAlpha, toPlatform.Alpha, alpha));
					}

					if (_proxyPlatformEnterTime > 0.0f)
					{
						float deltaTime  = Time.unscaledTime - platformEnter.Time;
						float deltaAlpha = _proxyPlatformEnterTime > 0.001f ? Mathf.Clamp01(deltaTime / _proxyPlatformEnterTime) : 1.0f;

						platformEnter.EnterAlpha += deltaAlpha;
						if (platformEnter.EnterAlpha > 1.0f)
						{
							platformEnter.EnterAlpha = 1.0f;
						}

						platformEnter.KCCOffset = Vector3.Lerp(platformEnter.KCCOffset, platform.KCCOffset, deltaAlpha);
					}
				}
				else
				{
					// The interpolation can happen multiple times during the frame, we need to correctly track delta time since last processing (not last frame).
					float deltaTime = Time.unscaledTime - platformEnter.Time;

					platformEnter.StateAlpha -= _worldSpaceTransitionDuration > 0.001f ? (deltaTime / _worldSpaceTransitionDuration) : 1.0f;
					if (platformEnter.StateAlpha <= 0.0f)
					{
						_platformEnters.RemoveAt(i);
						continue;
					}
				}

				platformEnter.Time = Time.unscaledTime;

				_platformEnters[i] = platformEnter;
			}

			// 2. Create a PlatformEnter instances for every platform in Active state.
			for (int i = 0; i < MAX_PLATFORMS; ++i)
			{
				Platform platform = _state.Platforms.Get(i);
				if (platform.State == EPlatformState.Active)
				{
					if (HasPlatformEnter(platform.Id) == true)
						continue;
					if (TryGetPlatformTransform(platform.Id, i, out Vector3 platformPosition, out Quaternion platformRotation) == false)
						continue;

					// A platform has been entered in the [Networked] state.
					PlatformEnter platformEnter = new PlatformEnter();
					platformEnter.Id         = platform.Id;
					platformEnter.Time       = Time.unscaledTime;
					platformEnter.StateAlpha = 1.0f;
					platformEnter.EnterAlpha = 0.0f;
					platformEnter.KCCOffset  = platform.KCCOffset;

					_platformEnters.Add(platformEnter);
				}
			}
		}

		private void ProcessPlatformExits(ProcessorState fromState, ProcessorState toState, float alpha)
		{
			// 1. Remove all platform exits which are reactivated or no longer tracked by interpolated state.
			for (int i = _platformExits.Count - 1; i >= 0; --i)
			{
				NetworkId networkId = _platformExits[i].Id;

				if (_state.GetPlatformState(networkId) == EPlatformState.Active)
				{
					_platformExits.RemoveAt(i);
					continue;
				}

				if (fromState.HasPlatform(networkId) == false && toState.HasPlatform(networkId) == false)
				{
					_platformExits.RemoveAt(i);
					continue;
				}
			}

			// 2. Create a PlatformExit instances for every platform in Inactive state.
			for (int i = 0; i < MAX_PLATFORMS; ++i)
			{
				Platform platform = _state.Platforms.Get(i);
				if (platform.State == EPlatformState.Inactive)
				{
					if (HasPlatformExit(platform.Id) == true)
						continue;
					if (TryGetPlatformTransform(platform.Id, i, out Vector3 platformPosition, out Quaternion platformRotation) == false)
						continue;

					// A platform has been left in the [Networked] state.
					PlatformExit platformExit = new PlatformExit();
					platformExit.Id       = platform.Id;
					platformExit.Position = platformPosition;
					platformExit.Rotation = platformRotation;

					_platformExits.Add(platformExit);
				}
			}
		}

		private void GetPlatformSpacePositionAndVelocity(Vector3 worldSpacePosition, Vector3 worldSpaceVelocity, ProcessorState state, out Vector3 platformSpacePosition, out Vector3 platformSpaceVelocity)
		{
			float   targetAlpha    = default;
			Vector3 targetPosition = default;
			Vector3 targetVelocity = default;

			// Get positions and velocities from all platforms.
			for (int i = 0; i < MAX_PLATFORMS; ++i)
			{
				Platform platform = state.Platforms.Get(i);
				if (platform.State == EPlatformState.None)
					continue;

				Vector3    platformPosition;
				Quaternion platformRotation;

				if (TryGetPlatformExit(platform.Id, out PlatformExit platformExit) == true)
				{
					platformPosition = platformExit.Position;
					platformRotation = platformExit.Rotation;
				}
				else
				{
					if (TryGetPlatformTransform(platform.Id, i, out platformPosition, out platformRotation) == false)
						continue;
				}

				Vector3 kccPosition = platformPosition + platformRotation * platform.KCCOffset;

				targetAlpha    += platform.Alpha;
				targetPosition += kccPosition * platform.Alpha;
				targetVelocity += platform.KCCVelocity * platform.Alpha;
			}

			if (targetAlpha <= 0.0f)
			{
				// There's no active platform.
				platformSpacePosition = worldSpacePosition;
				platformSpaceVelocity = worldSpaceVelocity;
				return;
			}

			if (targetAlpha < 1.0f)
			{
				// The KCC is in a process of entering/leaving platform(s). Values are interpolated between world and platform spaces.
				platformSpacePosition = targetPosition + worldSpacePosition * (1.0f - targetAlpha);
				platformSpaceVelocity = targetVelocity + worldSpaceVelocity * (1.0f - targetAlpha);
				return;
			}

			// The KCC is fully platform space interpolated or there's an active transition between multiple platforms.
			platformSpacePosition = targetPosition / targetAlpha;
			platformSpaceVelocity = targetVelocity / targetAlpha;
		}

		private void ApplyPreEnterOffset(KCCData data)
		{
			float   targetAlpha    = default;
			Vector3 targetPosition = default;

			for (int i = 0, count = _platformEnters.Count; i < count; ++i)
			{
				PlatformEnter platformEnter = _platformEnters[i];
				if (platformEnter.StateAlpha <= 0.0f)
					continue;
				if (platformEnter.EnterAlpha <= 0.0f)
					continue;
				if (TryGetPlatformTransform(platformEnter.Id, -1, out Vector3 platformPosition, out Quaternion platformRotation) == false)
					continue;

				float oneMinusStateAlpha = 1.0f - platformEnter.StateAlpha;
				float stateAlphaInvPow3  = 1.0f - oneMinusStateAlpha * oneMinusStateAlpha * oneMinusStateAlpha;

				// 1. Calculate position of the KCC relative to the platform.
				Vector3 kccPosition = platformPosition + platformRotation * platformEnter.KCCOffset;

				// 2. Based on EnterAlpha, the position is interpolated between original KCC world space interpolated position (0.0f) and position of the KCC in platform space (1.0f).
				kccPosition = Vector3.Lerp(data.TargetPosition, kccPosition, platformEnter.EnterAlpha);

				// 3. The candidate position is then interpolated between StateAlpha (0.0f = just entered the platform in interpolation buffers, 1.0f = fully in platform space in interpolation buffers).
				kccPosition = Vector3.Lerp(data.TargetPosition, kccPosition, stateAlphaInvPow3);

				targetAlpha    += stateAlphaInvPow3;
				targetPosition += kccPosition * stateAlphaInvPow3;
			}

			if (targetAlpha <= 0.0f)
				return;

			if (targetAlpha <= 1.0f)
			{
				targetPosition += data.TargetPosition * (1.0f - targetAlpha);
			}
			else
			{
				targetPosition /= targetAlpha;
			}

			data.TargetPosition = targetPosition;
		}

		// HELPER METHODS

		private Platform GetPlatform(int index, bool isInFixedUpdate)
		{
			return isInFixedUpdate == true ? _state.Platforms.Get(index) : _renderPlatforms[index];
		}

		private void SetPlatform(int index, Platform platform, bool isInFixedUpdate)
		{
			if (isInFixedUpdate == true)
			{
				_state.Platforms.Set(index, platform);
			}

			_renderPlatforms[index] = platform;
		}

		private void SynchronizeRenderPlatforms()
		{
			for (int i = 0; i < MAX_PLATFORMS; ++i)
			{
				_renderPlatforms[i] = _state.Platforms.Get(i);
			}
		}

		private bool HasPlatformTransform(NetworkId networkId)
		{
			for (int i = 0; i < MAX_PLATFORMS; ++i)
			{
				if (_platformTransforms[i].Id == networkId)
					return true;
			}

			return false;
		}

		private void CachePlatformTransforms()
		{
			for (int i = 0; i < MAX_PLATFORMS; ++i)
			{
				_platformTransforms[i] = default;

				Platform platform = _state.Platforms.Get(i);
				if (platform.Id.IsValid == false)
					continue;

				NetworkObject platformObject = Runner.FindObject(platform.Id);
				if (ReferenceEquals(platformObject, null) == true)
					continue;

				platformObject.transform.GetPositionAndRotation(out Vector3 platformPosition, out Quaternion platformRotation);

				PlatformTransform platformTransform = new PlatformTransform();
				platformTransform.Id       = platform.Id;
				platformTransform.Position = platformPosition;
				platformTransform.Rotation = platformRotation;

				_platformTransforms[i] = platformTransform;
			}
		}

		private bool TryGetPlatformTransform(NetworkId networkId, int cacheIndex, out Vector3 platformPosition, out Quaternion platformRotation)
		{
			if (cacheIndex >= 0)
			{
				PlatformTransform platformTransform = _platformTransforms[cacheIndex];
				if (platformTransform.Id == networkId)
				{
					platformPosition = platformTransform.Position;
					platformRotation = platformTransform.Rotation;
					return true;
				}
			}

			for (int i = 0; i < MAX_PLATFORMS; ++i)
			{
				PlatformTransform platformTransform = _platformTransforms[i];
				if (platformTransform.Id == networkId)
				{
					platformPosition = platformTransform.Position;
					platformRotation = platformTransform.Rotation;
					return true;
				}
			}

			NetworkObject platformObject = Runner.FindObject(networkId);
			if (ReferenceEquals(platformObject, null) == true)
			{
				platformPosition = Vector3.zero;
				platformRotation = Quaternion.identity;
				return false;
			}

			platformObject.transform.GetPositionAndRotation(out platformPosition, out platformRotation);
			return true;
		}

		private bool HasPlatformEnter(NetworkId networkId)
		{
			for (int i = 0, count = _platformEnters.Count; i < count; ++i)
			{
				if (_platformEnters[i].Id == networkId)
					return true;
			}

			return false;
		}

		private bool TryGetPlatformEnter(NetworkId networkId, out PlatformEnter platformEnter)
		{
			for (int i = 0, count = _platformEnters.Count; i < count; ++i)
			{
				PlatformEnter enter = _platformEnters[i];
				if (enter.Id == networkId)
				{
					platformEnter = enter;
					return true;
				}
			}

			platformEnter = default;
			return false;
		}

		private bool HasPlatformExit(NetworkId networkId)
		{
			for (int i = 0, count = _platformExits.Count; i < count; ++i)
			{
				if (_platformExits[i].Id == networkId)
					return true;
			}

			return false;
		}

		private bool TryGetPlatformExit(NetworkId networkId, out PlatformExit platformExit)
		{
			for (int i = 0, count = _platformExits.Count; i < count; ++i)
			{
				PlatformExit exit = _platformExits[i];
				if (exit.Id == networkId)
				{
					platformExit = exit;
					return true;
				}
			}

			platformExit = default;
			return false;
		}

		// DATA STRUCTURES

		public enum EPlatformState
		{
			None     = 0,
			Active   = 1, // The KCC is inside the platform's collider.
			Inactive = 2, // The KCC is outside the platform's collider. There is a pending transition to a world space or other platform space.
		}

		// Used to store information about a platform the KCC interacts with.
		public struct Platform : INetworkStruct
		{
			public NetworkId      Id;
			public EPlatformState State;
			public float          Alpha;
			public Vector3        Position;
			public Quaternion     Rotation;
			public Vector3        KCCOffset;
			public Vector3        KCCVelocity;
		}

		// Used to cache platform transforms.
		private struct PlatformTransform
		{
			public NetworkId  Id;
			public Vector3    Position;
			public Quaternion Rotation;
		}

		// Used to track entering a platform (State => Active) based on latest [Networked] state snapshot.
		private struct PlatformEnter
		{
			public NetworkId Id;
			public float     Time;
			public float     StateAlpha;
			public float     EnterAlpha;
			public Vector3   KCCOffset;
		}

		// Used to track leaving a platform (State => Inactive) based on latest [Networked] state snapshot.
		private struct PlatformExit
		{
			public NetworkId  Id;
			public Vector3    Position;
			public Quaternion Rotation;
		}

		public struct ProcessorState : INetworkStruct
		{
			public int Flags;
			[Networked][Capacity(MAX_PLATFORMS)]
			public NetworkArray<Platform> Platforms => default;

			public bool HasPlatforms { get => (Flags & 1) == 1; set { if (value) { Flags |= 1; } else { Flags &= ~1; } } }

			public bool HasPlatform(NetworkId networkId)
			{
				for (int i = 0; i < MAX_PLATFORMS; ++i)
				{
					if (Platforms.Get(i).Id == networkId)
						return true;
				}

				return false;
			}

			public bool TryGetPlatform(NetworkId networkId, out Platform platform)
			{
				for (int i = 0; i < MAX_PLATFORMS; ++i)
				{
					Platform statePlatform = Platforms.Get(i);
					if (statePlatform.Id == networkId)
					{
						platform = statePlatform;
						return true;
					}
				}

				platform = default;
				return false;
			}

			public EPlatformState GetPlatformState(NetworkId networkId)
			{
				for (int i = 0; i < MAX_PLATFORMS; ++i)
				{
					Platform platform = Platforms.Get(i);
					if (platform.Id == networkId)
						return platform.State;
				}

				return EPlatformState.None;
			}
		}
	}
}
