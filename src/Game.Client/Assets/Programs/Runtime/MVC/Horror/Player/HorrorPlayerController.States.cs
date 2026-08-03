using Cysharp.Threading.Tasks;
using Game.Horror.Interaction;
using Game.Library.Shared;
using Game.Shared.Enums;
using UnityEngine;

namespace Game.Horror.Player
{
    public partial class HorrorPlayerController
    {
        #region StateMachine

        private void InitializeStateMachine()
        {
            _stateMachine = new StateMachine<HorrorPlayerController, StateEvent>(this);

            // 状態遷移テーブルの構築
            _stateMachine.AddTransition<IdleState, MovingState>(StateEvent.Move);
            _stateMachine.AddTransition<MovingState, IdleState>(StateEvent.Stop);

            _stateMachine.AddTransition<IdleState, JumpingState>(StateEvent.Jump);
            _stateMachine.AddTransition<MovingState, JumpingState>(StateEvent.Jump);

            _stateMachine.AddTransition<JumpingState, IdleState>(StateEvent.Land);

            _stateMachine.AddTransition<IdleState, InteractingState>(StateEvent.Interact);
            _stateMachine.AddTransition<MovingState, InteractingState>(StateEvent.Interact);
            _stateMachine.AddTransition<InteractingState, IdleState>(StateEvent.EndInteract);

            _stateMachine.AddTransition<IdleState, AttackingState>(StateEvent.Attack);
            _stateMachine.AddTransition<MovingState, AttackingState>(StateEvent.Attack);
            _stateMachine.AddTransition<AttackingState, IdleState>(StateEvent.EndAttack);

            _stateMachine.AddTransition<IdleState, EquippingState>(StateEvent.Equip);
            _stateMachine.AddTransition<MovingState, EquippingState>(StateEvent.Equip);
            _stateMachine.AddTransition<EquippingState, IdleState>(StateEvent.EndEquip);

            _stateMachine.AddTransition<IdleState, ReloadingState>(StateEvent.Reload);
            _stateMachine.AddTransition<MovingState, ReloadingState>(StateEvent.Reload);
            _stateMachine.AddTransition<ReloadingState, IdleState>(StateEvent.EndReload);

            _stateMachine.AddTransition<IdleState, UsingItemState>(StateEvent.UseItem);
            _stateMachine.AddTransition<MovingState, UsingItemState>(StateEvent.UseItem);
            _stateMachine.AddTransition<UsingItemState, IdleState>(StateEvent.EndUseItem);

            _stateMachine.AddTransition<IdleState>(StateEvent.Idle);
            _stateMachine.AddTransition<DeadState>(StateEvent.Dead);

            // 初期ステート
            _stateMachine.SetInitState<IdleState>();
        }

        /// <summary>
        /// 状態遷移イベントKey
        /// </summary>
        private enum StateEvent
        {
            Idle, // 待機状態: Idle
            Move, // 移動開始: Idle → Moving
            Stop, // 移動停止: Moving → Idle
            Jump, // ジャンプ: Idle/Moving → Jumping
            Land, // 着地: Jumping → Idle
            Interact, // インタラクト開始: Idle/Moving → Interacting
            EndInteract, // インタラクト終了: Interacting → Idle
            Attack, // 攻撃開始: Idle/Moving → Attacking
            EndAttack, // 攻撃終了（発射間隔経過）: Attacking → Idle
            Equip, // 装備切替開始: Idle/Moving → Equipping
            EndEquip, // 装備切替終了（EquipDuration経過）: Equipping → Idle
            Reload, // リロード開始: Idle/Moving → Reloading
            EndReload, // リロード終了（ReloadDuration経過）: Reloading → Idle
            UseItem, // アイテム使用開始: Idle/Moving → UsingItem
            EndUseItem, // アイテム使用終了（EffectApplyDuration経過）: UsingItem → Idle
            Dead,
        }

        private class IdleState : State<HorrorPlayerController, StateEvent>
        {
            public override void Update()
            {
                var ctx = Context;
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();
                ctx.UpdateAimPose();

                // ジャンプ入力チェック
                if (ctx._jumpTriggered && ctx.IsGrounded())
                {
                    StateMachine.Transition(StateEvent.Jump);
                    return;
                }

                // インタラクト起動チェック
                if (ctx.TryInteraction())
                {
                    StateMachine.Transition(StateEvent.Interact);
                    return;
                }

                // 攻撃（射撃）起動チェック
                if (ctx.TryAttack())
                {
                    StateMachine.Transition(StateEvent.Attack);
                    return;
                }

                // アイテム使用起動チェック（装備予約と併存した場合は回復を先に実行する）
                if (ctx.TryUseItem())
                {
                    StateMachine.Transition(StateEvent.UseItem);
                    return;
                }

                // 装備切替起動チェック
                if (ctx.TryEquip())
                {
                    StateMachine.Transition(StateEvent.Equip);
                    return;
                }

                // リロード起動チェック
                if (ctx.TryReload())
                {
                    StateMachine.Transition(StateEvent.Reload);
                    return;
                }

                // 移動入力チェック
                if (ctx.IsMoveInput())
                {
                    StateMachine.Transition(StateEvent.Move);
                }
            }

            public override void FixedUpdate()
            {
                // 静止中も重力を適用
                Context.UpdateMovementWithGravity(Vector3.zero);
            }
        }

        private class MovingState : State<HorrorPlayerController, StateEvent>
        {
            public override void Update()
            {
                var ctx = Context;
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();
                ctx.UpdateAimPose();

                // ジャンプ入力チェック
                if (ctx._jumpTriggered && ctx.IsGrounded())
                {
                    StateMachine.Transition(StateEvent.Jump);
                    return;
                }

                // インタラクト起動チェック
                if (ctx.TryInteraction())
                {
                    StateMachine.Transition(StateEvent.Interact);
                    return;
                }

                // 攻撃（射撃）起動チェック
                if (ctx.TryAttack())
                {
                    StateMachine.Transition(StateEvent.Attack);
                    return;
                }

                // アイテム使用起動チェック（装備予約と併存した場合は回復を先に実行する）
                if (ctx.TryUseItem())
                {
                    StateMachine.Transition(StateEvent.UseItem);
                    return;
                }

                // 装備切替起動チェック
                if (ctx.TryEquip())
                {
                    StateMachine.Transition(StateEvent.Equip);
                    return;
                }

                // リロード起動チェック
                if (ctx.TryReload())
                {
                    StateMachine.Transition(StateEvent.Reload);
                    return;
                }

                // 移動入力がなくなったらIdleへ
                if (!ctx.IsMoveInput())
                {
                    StateMachine.Transition(StateEvent.Stop);
                }
            }

            public override void FixedUpdate()
            {
                var ctx = Context;
                ctx.UpdateMovementWithGravity(ctx.ComputeHorizontalVelocity());
            }
        }

        private class JumpingState : State<HorrorPlayerController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx._verticalVelocity = ctx.PlayerMaster.Jump;
                ctx._jumpTriggered = false;
            }

            public override void Update()
            {
                var ctx = Context;
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();
                ctx.UpdateAimPose();

                // 上昇終了 + 接地で着地判定
                if (ctx._verticalVelocity <= 0f && ctx.IsGrounded())
                {
                    StateMachine.Transition(StateEvent.Land);
                }
            }

            public override void FixedUpdate()
            {
                var ctx = Context;
                // 空中でも水平移動を許可
                ctx.UpdateMovementWithGravity(ctx.ComputeHorizontalVelocity());
            }
        }

        /// <summary>
        /// インタラクト実行中の身体占有状態。視点回転とエイム解除の補間のみ許可し水平移動は止める。
        /// 入力タイプを問わず、拒否メッセージ／単発・トグル／長押しを 1 本の非同期シーケンスで処理する。
        /// </summary>
        private class InteractingState : State<HorrorPlayerController, StateEvent>
        {
            private bool _completed;

            public override void Enter()
            {
                _completed = false;
                RunAsync(Context._interactTarget).Forget();
            }

            public override void Update()
            {
                Context.UpdateRotation(); // 拘束中は視点回転とエイム解除の補間のみ許可
                Context.UpdateAimPose();
                if (_completed) StateMachine.Transition(StateEvent.EndInteract);
            }

            // 水平移動なし＝拘束（重力のみ適用）
            public override void FixedUpdate() => Context.UpdateMovementWithGravity(Vector3.zero);

            public override void Exit()
            {
                var ctx = Context;
                ctx._interactTarget?.SetHoldProgress(0f); // 中断・完了とも即非表示
                ctx._interactTarget = null;
                _completed = false;
            }

            // 1 回のインタラクトを開始～効果発火まで逐次処理する。
            // 拒否（メッセージ）／単発・トグル（即時）／長押し（進捗）を 1 本のフローで扱う。
            private async UniTask RunAsync(IInteractable target)
            {
                if (!target.CanInteract())
                {
                    await target.TryShowRejectionMessage();
                }
                else if (target.InputType == InteractionInputType.Hold)
                {
                    await RunHoldAsync(target);
                }
                else
                {
                    target.Interact();
                }

                _completed = true;
            }

            private async UniTask RunHoldAsync(IInteractable target)
            {
                var ctx = Context;
                var elapsed = 0f;
                target.SetHoldProgress(0f);

                while (true)
                {
                    // 中断条件：対象喪失 / 視線を外した / ボタン解放 / 実行不可化 / 死亡
                    var stillAimed = ctx._interactionDetector != null
                                     && ctx._interactionDetector.TryGetTarget(out var current)
                                     && current == target;
                    if (!stillAimed || !ctx.Player.Interact.IsPressed() || !target.CanInteract() || ctx.IsDead)
                        return;

                    elapsed += Time.deltaTime;
                    target.SetHoldProgress(CalculateHoldProgress(elapsed, target.HoldSeconds));

                    if (elapsed >= target.HoldSeconds)
                    {
                        target.Interact();
                        return;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }
        }

        /// <summary>
        /// 射撃実行中の状態。Enter で 1 発発砲し、FireInterval（武器マスター）の間は移動・視点を許可しつつ
        /// 次弾の発射を待たせる（発射レート制限）。間隔を消化したら Idle へ戻る。
        /// </summary>
        private class AttackingState : State<HorrorPlayerController, StateEvent>
        {
            private float _elapsed;

            public override void Enter()
            {
                // インスタンスはキャッシュ再利用されるため経過時間を必ずリセット
                _elapsed = 0f;
                Context.Fire();
            }

            public override void Update()
            {
                var ctx = Context;
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();
                ctx.UpdateAimPose();

                _elapsed += Time.deltaTime;
                if (_elapsed >= ctx.GetFireInterval())
                    StateMachine.Transition(StateEvent.EndAttack);
            }

            public override void FixedUpdate()
            {
                var ctx = Context;
                ctx.UpdateMovementWithGravity(ctx.ComputeHorizontalVelocity());
            }
        }

        /// <summary>
        /// 装備切替実行中の状態。Enter で装備をセーブデータへ反映し、EquipDuration（武器マスター）の間は
        /// 移動・視点を許可しつつ硬直として滞在する。滞在秒を消化したら Idle へ戻る。
        /// </summary>
        private class EquippingState : State<HorrorPlayerController, StateEvent>
        {
            private float _elapsed;

            public override void Enter()
            {
                // インスタンスはキャッシュ再利用されるため経過時間を必ずリセット
                _elapsed = 0f;

                var ctx = Context;
                if (ctx._equipmentService.TryEquip(ctx._pendingEquipType, ctx._pendingEquipId))
                {
                    ctx._weaponView.BeginSwitch(ctx._pendingWeaponMaster);
                    ctx._equipmentsView.Show(ctx._pendingEquipType, ctx._pendingEquipId);
                    Debug.Log($"{ctx._pendingWeaponMaster.Name}");
                }
                else
                {
                    // 直前フレームの TryPrepareEquip で検証済みのため通常プレイでは到達しない（到達＝不変条件違反）
                    Debug.LogError($"装備反映に失敗しました ({ctx._pendingEquipType}, {ctx._pendingEquipId})");
                }
            }

            public override void Update()
            {
                var ctx = Context;
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();

                _elapsed += Time.deltaTime;
                ctx._weaponView.TickSwitch(_elapsed, ctx._pendingWeaponMaster.EquipDuration);
                ctx.UpdateAimPose(); // TickSwitch の後に呼ぶ（下げ量更新 → 位置反映の順序）
                if (_elapsed >= ctx._pendingWeaponMaster.EquipDuration)
                    StateMachine.Transition(StateEvent.EndEquip);
            }

            public override void FixedUpdate()
            {
                var ctx = Context;
                ctx.UpdateMovementWithGravity(ctx.ComputeHorizontalVelocity());
            }
        }

        /// <summary>
        /// リロード実行中の状態。ReloadDuration（武器マスター）の間、移動・視点・エイムを許可しつつ硬直として滞在し、
        /// 武器を傾ける演出を進める。滞在秒を消化した時点で装填（弾倉回復・予備消費）を適用して Idle へ戻る。
        /// 攻撃・ジャンプ・インタラクトの起動は入力側・遷移構造で禁止される。
        /// </summary>
        private class ReloadingState : State<HorrorPlayerController, StateEvent>
        {
            private float _elapsed;
            private float _duration;
            private bool _applied;

            public override void Enter()
            {
                var ctx = Context;
                // インスタンスはキャッシュ再利用されるため経過時間・適用済みフラグを必ずリセット
                _elapsed = 0f;
                _applied = false;
                _duration = ctx.EquippedWeaponMaster.ReloadDuration;
                ctx.NotifyHudViews();
            }

            public override void Update()
            {
                var ctx = Context;
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();

                _elapsed += Time.deltaTime;
                ctx._weaponView.TickReload(_elapsed, _duration);
                ctx.UpdateAimPose(); // TickReload の後に呼ぶ（傾き量更新 → 反映の順序）

                if (!_applied && _elapsed >= _duration)
                {
                    _applied = true; // フレーム落ち・将来の中断遷移追加に対する二重適用防止
                    ctx.ApplyReload();
                    StateMachine.Transition(StateEvent.EndReload);
                }
            }

            public override void FixedUpdate()
            {
                var ctx = Context;
                ctx.UpdateMovementWithGravity(ctx.ComputeHorizontalVelocity());
            }

            public override void Exit()
            {
                // 中断・完了とも傾き演出を確実に解除する
                Context._weaponView.ResetReload();
            }
        }

        /// <summary>
        /// アイテム使用（回復）実行中の状態。EffectApplyDuration（アイテムマスター）の間、移動・視点を許可しつつ
        /// 硬直として滞在し、経過比率に応じた回復を毎フレーム差分適用する（漸進適用。完了時一括ではない）。
        /// アイテムは開始時点で消費するため、死亡による中断時は途中までの回復適用・消費済みのまま終わる。
        /// 攻撃・リロード・インタラクトの起動は入力側・遷移構造で禁止される。
        /// </summary>
        private class UsingItemState : State<HorrorPlayerController, StateEvent>
        {
            private float _elapsed;
            private int _appliedHeal;

            public override void Enter()
            {
                var ctx = Context;
                // インスタンスはキャッシュ再利用されるため経過時間・適用済み量を必ずリセット
                _elapsed = 0f;
                _appliedHeal = 0;

                // 開始時点で指定スロットからの消費を確定する。TryUseItem の所持検証と同一フレームのため、失敗はデータ異常時のみの防御パス
                if (!ctx._inventoryService.TryConsumeAt(ObjectCategory.Item, ctx._pendingUseItemMaster.Id, ctx._pendingUseSlotNo, 1))
                {
                    Debug.LogError($"アイテム消費に失敗したため使用を中止します Id={ctx._pendingUseItemMaster.Id} SlotNo={ctx._pendingUseSlotNo}", ctx);
                    StateMachine.Transition(StateEvent.EndUseItem);
                }
            }

            public override void Update()
            {
                var ctx = Context;
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();

                _elapsed += Time.deltaTime;
                ApplyProgressiveHeal(ctx);
                ctx.UpdateAimPose();

                if (_elapsed >= ctx._pendingUseItemMaster.EffectApplyDuration)
                    StateMachine.Transition(StateEvent.EndUseItem);
            }

            public override void FixedUpdate()
            {
                var ctx = Context;
                ctx.UpdateMovementWithGravity(ctx.ComputeHorizontalVelocity());
            }

            public override void Exit()
            {
                // 中断（死亡）時も残量の一括適用はしない（途中までの回復のみが残る）
                Context._pendingUseItemMaster = null;
                Context._pendingUseSlotNo = -1;
            }

            // 経過比率から適用済み総量を再計算し、前フレームとの差分のみを加算する（丸め誤差の蓄積防止）
            private void ApplyProgressiveHeal(HorrorPlayerController ctx)
            {
                var master = ctx._pendingUseItemMaster;
                var applied = CalculateAppliedHeal(master.Effect, _elapsed, master.EffectApplyDuration);
                var diff = applied - _appliedHeal;
                if (diff <= 0) return;

                _appliedHeal = applied;
                ctx.ApplyHealth(CalculateHealedHealth(ctx._playerService.CurrentHealth, diff, ctx._playerService.MaxHealth));
            }
        }

        /// <summary>
        /// 死亡状態（終端）
        /// 復帰は GameOverDialog のシーン遷移＝プレイヤー再生成で行われる）。
        /// 入力は UpdateInput の restrained（死亡込み）で遮断し、水平移動を止め重力のみ適用する。
        /// Enter で演出ディレイ → HorrorGameOverDialog.RunAsync のシーケンスを起動する。
        /// </summary>
        private class DeadState : State<HorrorPlayerController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;

                // 遷移フレームからエイム解除の補間を開始する（他の入力フラグは終端ステートでは読まれないため触らない。
                // 次フレーム以降は UpdateInput の restrained 分岐が毎フレーム解除を保証する）
                ctx._isAiming = false;

                // ヘッドボブの残オフセットを rest 位置へ戻す（以降 UpdateHeadBob は呼ばない）
                if (ctx._mainCamera != null)
                    ctx._mainCamera.transform.localPosition = ctx._cameraBasePosition;

                ctx.RunGameOverAsync().Forget();
            }

            // 視点回転なし。エイム解除補間・FOV 復帰・残弾 HUD フェードアウトのみ進める
            public override void Update() => Context.UpdateAimPose();

            // 水平移動なし＝拘束（重力のみ適用）— InteractingState と同じ
            public override void FixedUpdate() => Context.UpdateMovementWithGravity(Vector3.zero);
        }

        #endregion
    }
}
