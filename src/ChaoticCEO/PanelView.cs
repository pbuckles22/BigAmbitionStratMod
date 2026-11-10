using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ChaoticCEO
{
    [DefaultExecutionOrder(-32000)]
    public sealed class PanelView : MonoBehaviour
    {
        private const int WindowId = 0xCE01;

        private static PanelView? _instance;

        private readonly PanelSession _session = new PanelSession();

        private Rect _window = new Rect(40f, 72f, 920f, 680f);

        private Vector2 _scroll;

        private IReadOnlyList<PanelLine> _lines = Array.Empty<PanelLine>();

        private IReadOnlyList<string> _draw = Array.Empty<string>();

        private string _localAppData = "";

        private string _pendingStreet = "";

        private int _pendingNumber;

        private string _pendingRedo = "";

        private Texture2D? _backdrop;

        private GUIStyle? _label;

        private GUIStyle? _windowStyle;

        private bool _eating;

        private bool _pointerInside;

        private bool _f9Held;

        private float _held;

        private int _boardFrame = -1;

        private CityBoard? _board;

        private string _lastFailure = "";

        private string _bar = "";

        private string _health = "";

        private string _playbook = "";

        public static PanelView Ensure(string localAppData)
        {
            if (_instance == null)
            {
                var host = new GameObject("ChaoticCEO.Panel");
                host.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<PanelView>();
                _instance.HookScroll();
            }

            _instance._localAppData = localAppData ?? "";
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
            _localAppData = localAppData ?? "";
            if (!LiveDump.InCity())
            {
                Note("panel.skipped reason=nocurrent");
                return;
            }

            _boardFrame = -1;
            _session.Press(_localAppData, LiveDump.Board());
            if (!_session.IsOpen)
            {
                _pendingStreet = "";
                _pendingNumber = 0;
                _pendingRedo = "";
            }
        }

        private void OnGUI()
        {
            if (!_session.IsOpen)
            {
                return;
            }

            try
            {
                DrawGui();
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void DrawGui()
        {
            if (!LiveDump.InCity())
            {
                _session.Close();
                _pendingStreet = "";
                _pendingNumber = 0;
                _pendingRedo = "";
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
            _lines = _session.Draw(BoardNow());
            _draw = Panel.Text(_lines);
            _bar = LiveDump.HudLine();
            TopBar.Record(_localAppData, _bar);
            _health = Health.Latest(_localAppData);
            Health.Record(_localAppData, _health);
            var playbook = LiveDump.NextPlaybook(_localAppData);
            _playbook = Playbook.Line(playbook);
            Playbook.Record(_localAppData, playbook);
            Controls.Record(_localAppData, Controls.Row(Autopilot.Enabled, Autopilot.DryRun));
            GUI.DrawTexture(_window, _backdrop);
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "ChaoticCEO", _windowStyle);
            var pointer = Event.current == null ? Vector2.zero : Event.current.mousePosition;
            _pointerInside = _window.Contains(pointer);
            GUI.color = color;
            GUI.contentColor = content;
            GUI.backgroundColor = background;
        }

        private void Update()
        {
            var f9 = false;
            try
            {
                f9 = F9Held();
            }
            catch (Exception ex)
            {
                Fail(ex);
            }

            if (f9 && !_f9Held)
            {
                try
                {
                    Press(_localAppData);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                }
            }

            _f9Held = f9;
            var held = Panel.HoldWheel(_session.IsOpen, _pointerInside, Input.mouseScrollDelta.y);
            if (held == 0f)
            {
                return;
            }

            _held += held;
            Input.ResetInputAxes();
            var mouse = Mouse.current;
            if (mouse != null)
            {
                InputState.Change(mouse.scroll, Vector2.zero);
            }
        }

        private void DrawWindow(int id)
        {
            GUI.DrawTexture(new Rect(0f, 0f, _window.width, _window.height), _backdrop);
            var evt = Event.current;
            if (evt != null && evt.type == EventType.ScrollWheel && _window.Contains(evt.mousePosition))
            {
                evt.Use();
            }

            if (evt != null && evt.type == EventType.Layout && _held != 0f)
            {
                _scroll.y += Panel.WheelStep(_held);
                _held = 0f;
            }

            if (_bar.Length > 0)
            {
                GUILayout.Label(_bar, _label);
            }

            if (_health.Length > 0)
            {
                GUILayout.Label(_health, _label);
            }

            if (_playbook.Length > 0)
            {
                GUILayout.Label(_playbook, _label);
            }

            _scroll = GUILayout.BeginScrollView(_scroll);
            var lineIndex = 0;
            for (var index = 0; index < _draw.Count; index++)
            {
                GUILayout.Label(_draw[index], _label);
                if ((index % 2) == 1)
                {
                    if (lineIndex < _lines.Count)
                    {
                        DrawClose(_lines[lineIndex]);
                        DrawRedo(_lines[lineIndex]);
                        DrawExecute(_lines[lineIndex]);
                    }

                    lineIndex++;
                    GUILayout.Space(8f);
                }
            }

            GUILayout.EndScrollView();
            DrawWatch();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
        }

        private void DrawWatch()
        {
            var captions = Controls.Buttons(Autopilot.Enabled, Autopilot.DryRun);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(captions[0]))
            {
                Controls.Toggle();
            }

            if (GUILayout.Button(captions[1]))
            {
                Controls.Dry();
            }

            if (GUILayout.Button(captions[2]))
            {
                Controls.Append(_localAppData, Controls.Pressed(Controls.PressAll()));
            }

            if (GUILayout.Button(captions[3]))
            {
                Controls.ResumeSkip(_localAppData);
                _session.Close();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawExecute(PanelLine line)
        {
            if (line == null || line.Business != "step")
            {
                return;
            }

            var buttons = Controls.Execute(new[] { line });
            if (buttons.Count == 0)
            {
                return;
            }

            if (GUILayout.Button(buttons[0].Label))
            {
                Controls.Append(_localAppData, Controls.Pressed(Controls.Press(buttons[0])));
            }
        }

        private void DrawClose(PanelLine line)
        {
            if (line == null || line.Tone != "close" || line.Business != "step")
            {
                return;
            }

            var armed = _pendingStreet == line.Street && _pendingNumber == line.Number;
            var controls = ClosePress.Controls(armed);
            if (!armed)
            {
                if (GUILayout.Button(controls[0]))
                {
                    _pendingStreet = line.Street;
                    _pendingNumber = line.Number;
                }

                return;
            }

            GUILayout.BeginHorizontal();
            var background = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.75f, 0.16f, 0.16f);
            var confirm = GUILayout.Button(controls[0]);
            GUI.backgroundColor = background;
            var cancel = GUILayout.Button(controls[1]);
            GUILayout.EndHorizontal();
            if (cancel)
            {
                _pendingStreet = "";
                _pendingNumber = 0;
                return;
            }

            if (!confirm)
            {
                return;
            }

            ClosePress.Press(
                _localAppData,
                GameApi.Live(),
                BoardNow(),
                line.Street,
                line.Number,
                true);
            _pendingStreet = "";
            _pendingNumber = 0;
        }

        private void DrawRedo(PanelLine line)
        {
            if (line == null || line.Tone != "move" || line.Business != "logistics")
            {
                return;
            }

            var destination = line.Street + " " + line.Number.ToString(CultureInfo.InvariantCulture);
            var armed = _pendingRedo == destination;
            var controls = RedoPress.Controls(armed);
            if (!armed)
            {
                if (GUILayout.Button(controls[0]))
                {
                    _pendingRedo = destination;
                }

                return;
            }

            GUILayout.BeginHorizontal();
            var background = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.75f, 0.16f, 0.16f);
            var confirm = GUILayout.Button(controls[0]);
            GUI.backgroundColor = background;
            var cancel = GUILayout.Button(controls[1]);
            GUILayout.EndHorizontal();
            if (cancel)
            {
                _pendingRedo = "";
                return;
            }

            if (!confirm)
            {
                return;
            }

            RedoPress.Press(
                _localAppData,
                GameApi.Live(),
                BoardNow(),
                destination,
                true);
            _pendingRedo = "";
        }

        private CityBoard BoardNow()
        {
            if (_boardFrame != Time.frameCount || _board == null)
            {
                _boardFrame = Time.frameCount;
                _board = LiveDump.Board();
            }

            return _board;
        }

        private static bool F9Held()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f9Key.isPressed)
            {
                return true;
            }

            return Input.GetKey(KeyCode.F9);
        }

        private void Fail(Exception ex)
        {
            Note("panel.failed " + ex.GetType().Name + " " + OneLine(ex.Message));
        }

        private void Note(string line)
        {
            if (line == _lastFailure || string.IsNullOrEmpty(_localAppData))
            {
                return;
            }

            _lastFailure = line;
            var path = LoadLog.CeoLogPath(_localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
        }

        private static string OneLine(string? message)
        {
            return (message ?? "").Replace("\r", " ").Replace("\n", " ");
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
