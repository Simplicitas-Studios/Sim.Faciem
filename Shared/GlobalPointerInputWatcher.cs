using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using TouchPhase = UnityEngine.TouchPhase;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sim.Faciem.Shared
{
    public static class GlobalPointerInputWatcher
    {
        private static GlobalPointerInputWatcherDriver s_driver;

        public static IDisposable Subscribe(Action<int> callback)
        {
            if (callback == null || !Application.isPlaying)
            {
                return Disposable.Empty;
            }

            EnsureDriver();

            return s_driver.PressedFrame
                .Subscribe(callback);
        }

        private static void EnsureDriver()
        {
            if (s_driver != null)
            {
                return;
            }

            var gameObject = new GameObject("[Sim.Faciem] GlobalPointerInputWatcher");
            gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            s_driver = gameObject.AddComponent<GlobalPointerInputWatcherDriver>();
        }

        private sealed class GlobalPointerInputWatcherDriver : MonoBehaviour
        {
            private readonly Subject<int> _pressedFrame = new();

            private int _pendingFrame = -1;

            public Observable<int> PressedFrame => _pressedFrame.AsObservable();

            private void Update()
            {
                if (DidPointerPressThisFrame())
                {
                    _pendingFrame = Time.frameCount;
                }
            }

            private void LateUpdate()
            {
                if (_pendingFrame != Time.frameCount)
                {
                    return;
                }

                _pressedFrame.OnNext(_pendingFrame);
                _pendingFrame = -1;
            }

            private static bool DidPointerPressThisFrame()
            {
#if ENABLE_INPUT_SYSTEM
                if (DidNewInputSystemPointerPressThisFrame())
                {
                    return true;
                }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetMouseButtonDown(0))
                {
                    return true;
                }

                if (Input.touchCount > 0)
                {
                    var touch = Input.GetTouch(0);
                    if (touch.phase == TouchPhase.Began)
                    {
                        return true;
                    }
                }
#endif

                return false;
            }

#if ENABLE_INPUT_SYSTEM
            private static bool DidNewInputSystemPointerPressThisFrame()
            {
                return Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
            }
#endif
        }
    }
}
