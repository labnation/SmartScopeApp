using ESuite.Drawables;
using LabNation.DeviceInterface.Devices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ESuite
{
    [Flags]
    public enum KeyboardModifiers
    {
        None = 0,
        Shift = 1,
        Alt = 2,
        Control = 4,
        Windows = 8
    }

    static class KeyboardExtension
    {
        static Dictionary<KeyboardModifiers, List<Keys>> ModifierKeyMap = new Dictionary<KeyboardModifiers, List<Keys>>()
        {
            { KeyboardModifiers.Alt, new List<Keys>() { Keys.LeftAlt, Keys.RightAlt } },
            { KeyboardModifiers.Control, new List<Keys>() { Keys.LeftControl, Keys.RightControl } },
            { KeyboardModifiers.Shift, new List<Keys>() { Keys.LeftShift, Keys.RightShift } },
            { KeyboardModifiers.Windows, new List<Keys>() { Keys.LeftWindows, Keys.RightWindows} },
        };

        static public bool HasCombo(this KeyboardState k, KeyCombo c)
        {
            if (!k.KeysDown.Contains(c.key))
                return false;
            if (c.modifiers == KeyboardModifiers.None)
                return true;
            foreach (KeyboardModifiers m in Enum.GetValues(typeof(KeyboardModifiers)))
            {
                if (m == KeyboardModifiers.None) continue;
                if (c.modifiers.HasFlag(m) && ModifierKeyMap[m].Intersect(k.KeysPressed).Count() == 0)
                    return false;
            }
            return true;
        }
    }

    class KeyboardState
    {
        public List<Keys> KeysPressed;
        public List<Keys> KeysDown;
        public List<Keys> KeysUp;
    }

    public struct KeyCombo
    {
        public KeyboardModifiers modifiers;
        public Keys key;
        public KeyCombo(Keys key, KeyboardModifiers modifiers = KeyboardModifiers.None)
        {
            this.key = key;
            this.modifiers = modifiers;
        }
        public static implicit operator KeyCombo(Keys k)
        {
            return new KeyCombo(k);
        }
    }

    class KeyboardShortcut
    {
        public List<KeyCombo> combos;
        public Action action;
        public KeyboardShortcut(Keys key, Action action) : this(new List<Keys> { key }, action) { }
        public KeyboardShortcut(List<Keys> keys, Action action) : this(keys.Select(x => (KeyCombo)x).ToList(), action) { }
        public KeyboardShortcut(KeyCombo combo, Action action) : this(new List<KeyCombo>() { combo }, action) { }
        public KeyboardShortcut(List<KeyCombo> combo, Action action)
        {
            this.combos = combo;
            this.action = action;
        }
    }


    internal partial class UIHandler
    {
        const float KEYBOARD_STEP_MOVE = 0.01f;
        const float KEYBOARD_STEP_VIEWPORT = 0.02f;
        const float KEYBOARD_STEP_TRIGGER = 0.02f;

        internal void HandleKey(List<Keys> keysPressed, List<Keys> keysDown, List<Keys> keysUp, Point mousePos)
        {
            /*FIXME: add keyhandling which *is* allowed when interaction is not here */

            KeyboardState k = new KeyboardState() { KeysPressed = keysPressed, KeysDown = keysDown, KeysUp = keysUp };

            if (!InteractionAllowed) return;

            if (EDrawable.focusedDrawable != null && EDrawable.focusedDrawable.keyboardHandler != null)
                if (EDrawable.focusedDrawable.keyboardHandler(EDrawable.focusedDrawable, k))
                    return;

            bool shiftDown = keysPressed.Contains(Keys.LeftShift) || keysPressed.Contains(Keys.RightShift);
            bool ctrlDown = keysPressed.Contains(Keys.LeftControl) || keysPressed.Contains(Keys.RightControl);
            bool altDown = keysPressed.Contains(Keys.LeftAlt) || keysPressed.Contains(Keys.RightAlt);
            bool cmdDown = keysPressed.Contains(Keys.LeftWindows) || keysPressed.Contains(Keys.RightWindows);

            if (keysDown.Contains(Keys.Home) || keysDown.Contains(Keys.NumPad7))
            {
                if (gm.Graphs[GraphType.Frequency].Grid.PositionInsideInteractiveArea(new Vector2(mousePos.X, mousePos.Y)))
                    PanZoomFreqGridHorizontal(2f, 0f, 0f);
                else
                    PanZoomGridHorizontal(2f, 0f, 0f, ctrlDown);
            }
            else if (keysDown.Contains(Keys.End) || keysDown.Contains(Keys.NumPad1))
            {
                if (gm.Graphs[GraphType.Frequency].Grid.PositionInsideInteractiveArea(new Vector2(mousePos.X, mousePos.Y)))
                    PanZoomFreqGridHorizontal(0.5f, 0f, 0f);
                else
                    PanZoomGridHorizontal(0.5f, 0f, 0f, ctrlDown);
            }

            else if (keysDown.Contains(Keys.PageUp) || keysDown.Contains(Keys.NumPad9))
            {
                UICallbacks.PanAndZoomGrid(null, new object[] { 
                    new Vector2(1f, 0.5f), new Vector2(), new Vector2(), false 
                });
                PanZoomEnd();
            }
            else if (keysDown.Contains(Keys.PageDown) || keysDown.Contains(Keys.NumPad3))
            {
                UICallbacks.PanAndZoomGrid(null, new object[] { 
                    new Vector2(1f, 2f), new Vector2(), new Vector2(), false 
                });
                PanZoomEnd();
            }
            else if (keysDown.Contains(Keys.P))
                TogglePanoramaByUser();
            else if (keysDown.Contains(Keys.I))
                PanZoomPanoramaFromPanorama(2f, 0f, float.NaN, false);
            else if (keysDown.Contains(Keys.O))
                PanZoomPanoramaFromPanorama(0.5f, 0f, float.NaN, false);
            else if (keysDown.Contains(Keys.Up) || keysDown.Contains(Keys.NumPad8))
            {
                if (ctrlDown)
                    MoveTriggerAnalogLevelRelative(KEYBOARD_STEP_TRIGGER);
                else
                    UICallbacks.PanAndZoomGrid(null, new object[] { 
                        new Vector2(1f, 1f), new Vector2(), new Vector2(0f, -KEYBOARD_STEP_MOVE), false
                    });

            }
            else if (keysDown.Contains(Keys.Down) || keysDown.Contains(Keys.NumPad2))
            {
                if (ctrlDown)
                    MoveTriggerAnalogLevelRelative(-KEYBOARD_STEP_TRIGGER);
                else
                    UICallbacks.PanAndZoomGrid(null, new object[] { 
                        new Vector2(1f, 1f), new Vector2(), new Vector2(0f, KEYBOARD_STEP_MOVE), false
                    });
            }
            else if (keysDown.Contains(Keys.Left) || keysDown.Contains(Keys.NumPad4))
            {
                if (ctrlDown)
                    PanZoomViewportFromPanorama(1f, -KEYBOARD_STEP_VIEWPORT, float.NaN, false);
                else
                    UICallbacks.PanAndZoomGrid(null, new object[] { 
                        new Vector2(1f, 1f), new Vector2(), new Vector2(-KEYBOARD_STEP_MOVE, 0f), false
                    });
            }
            else if (keysDown.Contains(Keys.Right) || keysDown.Contains(Keys.NumPad6))
            {
                if (ctrlDown)
                    PanZoomViewportFromPanorama(1f, KEYBOARD_STEP_VIEWPORT, float.NaN, false);
                else
                    UICallbacks.PanAndZoomGrid(null, new object[] { 
                        new Vector2(1f, 1f), new Vector2(), new Vector2(KEYBOARD_STEP_MOVE, 0f), false
                    });
            }
            else if (keysDown.Contains(Keys.Tab))
            {
                if (shiftDown)
                    UICallbacks.SelectPreviousChannel(null, null);
                else
                    UICallbacks.SelectNextChannel(null, null);
            }
            else if (keysDown.Contains(Keys.T))
                SetTriggerOnSelectedChannel();
            else if (keysDown.Contains(Keys.Back) || keysDown.Contains(Keys.Delete))
                HideActiveChannel();
            // F keys
            else if (keysDown.Contains(Keys.F1) || keysDown.Contains(Keys.OemQuestion))
                ToggleHelp();
            else if (keysDown.Contains(Keys.A))
                SetAcquisitionMode(AcquisitionMode.AUTO);
            else if (keysDown.Contains(Keys.S))
                SetAcquisitionMode(AcquisitionMode.SINGLE);
            else if (keysDown.Contains(Keys.D))
                SetAcquisitionMode(AcquisitionMode.NORMAL);
            else if (keysDown.Contains(Keys.Space))
                ToggleAcquisitionRunning();
            else if (keysDown.Contains(Keys.D0))
                UpdateTriggerOfSelectedChannel(DigitalTriggerValue.L, ctrlDown, shiftDown);
            else if (keysDown.Contains(Keys.D1))
                UpdateTriggerOfSelectedChannel(DigitalTriggerValue.H, ctrlDown, shiftDown);
            else if (keysDown.Contains(Keys.R))
                UpdateTriggerOfSelectedChannel(DigitalTriggerValue.R, ctrlDown, shiftDown);
            else if (keysDown.Contains(Keys.F))
                UpdateTriggerOfSelectedChannel(DigitalTriggerValue.F, ctrlDown, shiftDown);
            else if (keysDown.Contains(Keys.X))
                UpdateTriggerOfSelectedChannel(DigitalTriggerValue.X, ctrlDown, shiftDown);
            else if (keysDown.Contains(Keys.Q))
                UICallbacks.Quit(null, null);   
        }

        private Dictionary<KeyCombo, NumPadKey> numericKeyMap = new Dictionary<KeyCombo, NumPadKey>()
        {
            { (KeyCombo)Keys.D0, NumPadKey.N0}, { (KeyCombo)Keys.NumPad0, NumPadKey.N0}, 
            { (KeyCombo)Keys.D1, NumPadKey.N1}, { (KeyCombo)Keys.NumPad1, NumPadKey.N1}, 
            { (KeyCombo)Keys.D2, NumPadKey.N2}, { (KeyCombo)Keys.NumPad2, NumPadKey.N2}, 
            { (KeyCombo)Keys.D3, NumPadKey.N3}, { (KeyCombo)Keys.NumPad3, NumPadKey.N3}, 
            { (KeyCombo)Keys.D4, NumPadKey.N4}, { (KeyCombo)Keys.NumPad4, NumPadKey.N4}, 
            { (KeyCombo)Keys.D5, NumPadKey.N5}, { (KeyCombo)Keys.NumPad5, NumPadKey.N5}, 
            { (KeyCombo)Keys.D6, NumPadKey.N6}, { (KeyCombo)Keys.NumPad6, NumPadKey.N6}, 
            { (KeyCombo)Keys.D7, NumPadKey.N7}, { (KeyCombo)Keys.NumPad7, NumPadKey.N7}, 
            { (KeyCombo)Keys.D8, NumPadKey.N8}, { (KeyCombo)Keys.NumPad8, NumPadKey.N8}, 
            { (KeyCombo)Keys.D9, NumPadKey.N9}, { (KeyCombo)Keys.NumPad9, NumPadKey.N9}, 
            { (KeyCombo)Keys.OemComma, NumPadKey.Period },
            { (KeyCombo)Keys.OemPeriod, NumPadKey.Period },
            { (KeyCombo)Keys.Enter, NumPadKey.Confirm },
            { (KeyCombo)Keys.OemMinus, NumPadKey.PlusMinus },
            { (KeyCombo)Keys.Delete, NumPadKey.Clear },
            { (KeyCombo)Keys.OemBackslash, NumPadKey.Clear },
            { (KeyCombo)Keys.C, NumPadKey.ClearAll },
            { (KeyCombo)Keys.Back, NumPadKey.Clear },
            { (KeyCombo)Keys.P, NumPadKey.Pico },
            { (KeyCombo)Keys.U, NumPadKey.Micro },
            { (KeyCombo)Keys.N, NumPadKey.Nano },
            { new KeyCombo(Keys.M, KeyboardModifiers.Shift) , NumPadKey.Mega },
            { (KeyCombo)Keys.M, NumPadKey.Milli },
            { (KeyCombo)Keys.Space, NumPadKey.Unit },
            { (KeyCombo)Keys.K, NumPadKey.Kilo },
            { (KeyCombo)Keys.G, NumPadKey.Giga },
            { (KeyCombo)Keys.T, NumPadKey.Tera },
        };

        private Dictionary<KeyCombo, NumPadKey> alphaKeyMap = new Dictionary<KeyCombo, NumPadKey>()
        {
            { (KeyCombo)Keys.A, NumPadKey.A},
            { (KeyCombo)Keys.B, NumPadKey.B},
            { (KeyCombo)Keys.C, NumPadKey.C},
            { (KeyCombo)Keys.D, NumPadKey.D},
            { (KeyCombo)Keys.E, NumPadKey.E},
            { (KeyCombo)Keys.F, NumPadKey.F},
            { (KeyCombo)Keys.G, NumPadKey.G},
            { (KeyCombo)Keys.H, NumPadKey.H},
            { (KeyCombo)Keys.I, NumPadKey.I},
            { (KeyCombo)Keys.J, NumPadKey.J},
            { (KeyCombo)Keys.K, NumPadKey.K},
            { (KeyCombo)Keys.L, NumPadKey.L},
            { (KeyCombo)Keys.M, NumPadKey.M},
            { (KeyCombo)Keys.N, NumPadKey.N},
            { (KeyCombo)Keys.O, NumPadKey.O},
            { (KeyCombo)Keys.P, NumPadKey.P},
            { (KeyCombo)Keys.Q, NumPadKey.Q},
            { (KeyCombo)Keys.R, NumPadKey.R},
            { (KeyCombo)Keys.S, NumPadKey.S},
            { (KeyCombo)Keys.T, NumPadKey.T},
            { (KeyCombo)Keys.U, NumPadKey.U},
            { (KeyCombo)Keys.V, NumPadKey.V},
            { (KeyCombo)Keys.W, NumPadKey.W},
            { (KeyCombo)Keys.X, NumPadKey.X},
            { (KeyCombo)Keys.Y, NumPadKey.Y},
            { (KeyCombo)Keys.Z, NumPadKey.Z},

            { (KeyCombo)Keys.OemBackslash, NumPadKey.Backslash },
            { (KeyCombo)Keys.OemCloseBrackets, NumPadKey.CloseBrackets},
            { (KeyCombo)Keys.OemComma, NumPadKey.Period },
            { (KeyCombo)Keys.OemOpenBrackets, NumPadKey.OpenBrackets },
            { (KeyCombo)Keys.OemPeriod, NumPadKey.Period },
            { (KeyCombo)Keys.OemPipe, NumPadKey.Pipe },
            { (KeyCombo)Keys.OemPlus, NumPadKey.Plus },
            { (KeyCombo)Keys.OemMinus, NumPadKey.Minus },
            { (KeyCombo)Keys.OemQuestion, NumPadKey.Question },
            { (KeyCombo)Keys.OemQuotes, NumPadKey.Quotes },
            { (KeyCombo)Keys.OemSemicolon, NumPadKey.Semicolon },
            { (KeyCombo)Keys.OemTilde, NumPadKey.Tilde },

            { (KeyCombo)Keys.Enter, NumPadKey.Confirm },
            { (KeyCombo)Keys.Delete, NumPadKey.Clear },
            { (KeyCombo)Keys.Back, NumPadKey.Clear },            
            { (KeyCombo)Keys.Space, NumPadKey.Unit },
            { (KeyCombo)Keys.D0, NumPadKey.N0}, { (KeyCombo)Keys.NumPad0, NumPadKey.N0},
            { (KeyCombo)Keys.D1, NumPadKey.N1}, { (KeyCombo)Keys.NumPad1, NumPadKey.N1},
            { (KeyCombo)Keys.D2, NumPadKey.N2}, { (KeyCombo)Keys.NumPad2, NumPadKey.N2},
            { (KeyCombo)Keys.D3, NumPadKey.N3}, { (KeyCombo)Keys.NumPad3, NumPadKey.N3},
            { (KeyCombo)Keys.D4, NumPadKey.N4}, { (KeyCombo)Keys.NumPad4, NumPadKey.N4},
            { (KeyCombo)Keys.D5, NumPadKey.N5}, { (KeyCombo)Keys.NumPad5, NumPadKey.N5},
            { (KeyCombo)Keys.D6, NumPadKey.N6}, { (KeyCombo)Keys.NumPad6, NumPadKey.N6},
            { (KeyCombo)Keys.D7, NumPadKey.N7}, { (KeyCombo)Keys.NumPad7, NumPadKey.N7},
            { (KeyCombo)Keys.D8, NumPadKey.N8}, { (KeyCombo)Keys.NumPad8, NumPadKey.N8},
            { (KeyCombo)Keys.D9, NumPadKey.N9}, { (KeyCombo)Keys.NumPad9, NumPadKey.N9},
        };

        private bool numericKeyHandler(EDrawable numpad, KeyboardState ks)
        {
            IKeyboard keyb = (IKeyboard)numpad;
            if (ks.HasCombo((KeyCombo)Keys.Escape))
            {
                RemoveFloater(numpad);
                return true;
            }
            foreach (var kvp in numericKeyMap)
            {
                if (ks.HasCombo(kvp.Key))
                {
                    keyb.OnKeyPressed(kvp.Value);
                    break;
                }
            }
            //absorb all keys
            return true;
        }

        private bool alphaKeyHandler(EDrawable numpad, KeyboardState ks)
        {
            IKeyboard keyb = (IKeyboard)numpad;
            if (ks.HasCombo((KeyCombo)Keys.Escape))
            {
                keyb.OnKeyPressed(NumPadKey.Esc);
                return true;
            }
            foreach (var kvp in alphaKeyMap)
            {
                if (ks.HasCombo(kvp.Key))
                {
                    keyb.OnKeyPressed(kvp.Value);
                    break;
                }
            }
            //absorb all keys
            return true;
        }
    }
}
