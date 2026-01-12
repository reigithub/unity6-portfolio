using System;
using System.Linq;
using UnityEngine.LowLevel;

namespace Game.Core
{
    /// <summary>
    /// PlayerLoopSystemに処理を挟みたい時の修正用スコープ
    /// </summary>
    // 使用例:
    // using (var m = PlayerLoopSystemModifier.Create())
    // {
    //     m.InsertAfter<Initialization.UpdateCameraMotionVectors>(new PlayerLoopSystem
    //     {
    //         type = typeof(???),
    //         updateDelegate = ???
    //     });
    // }
    public struct PlayerLoopModifyScope : IDisposable
    {
        private PlayerLoopSystem _rootSystem;

        private PlayerLoopModifyScope(in PlayerLoopSystem rootSystem)
        {
            _rootSystem = rootSystem;
        }

        public static PlayerLoopModifyScope Create()
        {
            return new PlayerLoopModifyScope(PlayerLoop.GetCurrentPlayerLoop());
        }

        public void Update()
        {
            PlayerLoop.SetPlayerLoop(_rootSystem);
        }

        public void Dispose()
        {
            Update();
        }

        public bool InsertBefore<T>(in PlayerLoopSystem subSystem) where T : struct
        {
            return Insert<T>(0, subSystem, ref _rootSystem);
        }

        public bool InsertAfter<T>(in PlayerLoopSystem subSystem) where T : struct
        {
            return Insert<T>(1, subSystem, ref _rootSystem);
        }

        private static bool Insert<T>(int insertOffset, in PlayerLoopSystem subSystem, ref PlayerLoopSystem parentSystem)
            where T : struct
        {
            var subSystems = parentSystem.subSystemList?.ToList();
            if (subSystems == default) return false;

            bool found = false;

            for (int i = 0; i < subSystems.Count; i++)
            {
                var s = subSystems[i];
                if (s.type == typeof(T))
                {
                    found = true;
                    subSystems.Insert(i + insertOffset, subSystem);
                    break;
                }
            }

            if (!found)
            {
                for (int i = 0; i < subSystems.Count; i++)
                {
                    var s = subSystems[i];
                    if (Insert<T>(insertOffset, subSystem, ref s))
                    {
                        found = true;
                        subSystems[i] = s;
                        break;
                    }
                }
            }

            if (found)
                parentSystem.subSystemList = subSystems.ToArray();

            return found;
        }
    }
}