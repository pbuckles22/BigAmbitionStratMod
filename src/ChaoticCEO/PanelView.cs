using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ChaoticCEO
{
    public sealed class PanelView : MonoBehaviour
    {
        private const int WindowId = 0xCE01;

        private static PanelView? _instance;

        private readonly PanelSession _session = new PanelSession();

        private Rect _window = new Rect(40f, 72f, 920f, 680f);

        private Vector2 _scroll;

        private IReadOnlyList<string> _draw = Array.Empty<string>();

        private Texture2D? _backdrop;

        private GUIStyle? _label;

        private GUIStyle? _windowStyle;

        private bool _eating;

        private bool _pointerInside;

        public static PanelView Ensure()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var host = new GameObject("ChaoticCEO.Panel");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<PanelView>();
            _instance.HookScroll();
            return _instance;
        }

        public static void Release()
        {
            if (_instance == null)
            {
                return;
            }

            _instance.UnhookScroll();
            _instance.DropPaint();
            Destroy(_instance.gameObject);
            _instance = null;
        }

        public void Press(string localAppData)
        {
            if (!LiveDump.InCity())
            {
                return;
            }

            _session.Press(localAppData, LiveDump.OverlayRows());
        }

        private void OnGUI()
        {
            if (!_session.IsOpen)
            {
                return;
            }

            if (!LiveDump.InCity())
            {
                _session.Close();
                return;
            }

            EnsurePaint();
            if (_backdrop == null || _label == null || _windowStyle == null)
            {
                return;
            }

            var color = GUI.color;
            var content = GUI.contentColor;
            var background = GUI.backgroundColor;
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;
            _draw = Panel.Text(_session.Draw(LiveDump.OverlayRows()));
            GUI.DrawTexture(_window, _backdrop);
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "ChaoticCEO", _windowStyle);
            var pointer = Event.current == null ? Vector2.zero : Event.current.mousePosition;
            _pointerInside = _window.Contains(pointer);
            GUI.color = color;
            GUI.contentColor = content;
            GUI.backgroundColor = background;
        }

        private void DrawWindow(int id)
        {
            GUI.DrawTexture(new Rect(0f, 0f, _window.width, _window.height), _backdrop);
            _scroll = GUILayout.BeginScrollView(_scroll);
            for (var index = 0; index < _draw.Count; index++)
            {
                GUILayout.Label(_draw[index], _label);
                if ((index % 2) == 1)
                {
                    GUILayout.Space(8f);
                }
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
        }

        private void HookScroll()
        {
            if (_eating)
            {
                return;
            }

            InputSystem.onAfterUpdate += OnInput;
            _eating = true;
        }

        private void UnhookScroll()
        {
            if (!_eating)
            {
                return;
            }

            InputSystem.onAfterUpdate -= OnInput;
            _eating = false;
        }

        private void OnInput()
        {
            var mouse = Mouse.current;
            var scroll = mouse == null ? 0f : mouse.scroll.ReadValue().y;
            if (!Panel.EatScroll(_session.IsOpen && _pointerInside, scroll, 0f))
            {
                return;
            }

            InputState.Change(mouse!.scroll, Vector2.zero);
        }

        private void EnsurePaint()
        {
            if (_label != null && _windowStyle != null && _backdrop != null)
            {
                return;
            }

            var paint = Panel.Paint();
            _backdrop = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _backdrop.hideFlags = HideFlags.HideAndDontSave;
            _backdrop.SetPixel(0, 0, new Color(paint.Red, paint.Green, paint.Blue, paint.Alpha));
            _backdrop.Apply();

            var ink = new Color(paint.TextRed, paint.TextGreen, paint.TextBlue, 1f);
            _label = new GUIStyle
            {
                fontSize = paint.FontSize,
                wordWrap = true,
                richText = false,
                padding = new RectOffset(0, 8, 1, 1),
            };
            Ink(_label, ink);

            _windowStyle = new GUIStyle
            {
                fontSize = paint.FontSize + 2,
                padding = new RectOffset(16, 16, 32, 12),
            };
            _windowStyle.normal.background = _backdrop;
            _windowStyle.onNormal.background = _backdrop;
            Ink(_windowStyle, ink);
        }

        private void DropPaint()
        {
            if (_backdrop == null)
            {
                return;
            }

            Destroy(_backdrop);
            _backdrop = null;
            _label = null;
            _windowStyle = null;
        }

        private static void Ink(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }
    }
}
