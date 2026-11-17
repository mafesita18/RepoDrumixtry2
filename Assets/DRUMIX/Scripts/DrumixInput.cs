using UnityEngine;
using UnityEngine.XR;
// Sin "using UnityEngine.InputSystem": ese namespace también tiene CommonUsages/InputDevice y chocaba (CS0104).
// Lo del Input System va escrito completo (UnityEngine.InputSystem.Keyboard...).

namespace Drumix
{
    /// Controles de prueba sin configurar nada:
    /// PC: A S D F G H J K = piezas, Enter = aceptar (jugar / siguiente diálogo / botón principal), Esc = pausa o volver, P = pausa.
    /// Quest: A/X = bombo (en menús = aceptar), botón Menú del control izquierdo = pausa (el gatillo es para los botones de la UI).
    public class DrumixInput : MonoBehaviour
    {
        public bool keyboard = true;
        public bool xrButtons = true;
        bool lastPrimary, lastMenu;
        float menuGuard;

        void Update()
        {
            var game = DrumixGame.Instance;
            if (!game) return;
            if (keyboard) Keyboard(game);
            if (xrButtons) XR(game);
        }

        void Keyboard(DrumixGame game)
        {
            for (int i = 0; i < 8; i++)
                if (KeyDown(i)) Strike(game, (DrumixLane)i);
            if (EnterDown()) Submit(game);
            if (EscDown()) Escape(game);
            else if (PauseDown()) game.TogglePause();
        }

        /// Aceptar: si la UI tiene algo que hacer (diálogo, botón principal) lo hace ella; si no, empieza la canción.
        static void Submit(DrumixGame game)
        {
            var ui = DrumixUI.Instance;
            if (ui && ui.Submit()) return;
            if (!game.Paused && (game.State == DrumixState.Menu || game.State == DrumixState.Results)) game.StartSong();
        }
        static void Escape(DrumixGame game)
        {
            var ui = DrumixUI.Instance;
            if (ui && ui.Back()) return; // sub-pantalla (ajustes, niveles, calibración, Dante): volver
            if (game.State == DrumixState.Countdown || game.State == DrumixState.Playing) { game.TogglePause(); return; }
            if (game.State == DrumixState.Results) game.BackToMenu();
        }

        static void Strike(DrumixGame game, DrumixLane lane)
        {
            var pad = game.GetPad(lane);
            if (pad) pad.Strike(1f, false);
            else DrumixEvents.PadStruck(lane, 1f);
        }

        void XR(DrumixGame game)
        {
            bool primary = Button(XRNode.LeftHand, UnityEngine.XR.CommonUsages.primaryButton) || Button(XRNode.RightHand, UnityEngine.XR.CommonUsages.primaryButton);
            bool menu = Button(XRNode.LeftHand, UnityEngine.XR.CommonUsages.menuButton);
            bool idle = game.State == DrumixState.Menu || game.State == DrumixState.Results || game.Paused;
            menuGuard -= Time.unscaledDeltaTime;
            if (idle && menuGuard <= 0 && primary && !lastPrimary) { Submit(game); menuGuard = 0.4f; }
            else if (!idle && primary && !lastPrimary) Strike(game, DrumixLane.Kick);
            if (menu && !lastMenu) Escape(game);
            lastPrimary = primary; lastMenu = menu;
        }

        static bool Button(XRNode node, InputFeatureUsage<bool> usage)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            bool value;
            return device.isValid && device.TryGetFeatureValue(usage, out value) && value;
        }

#if ENABLE_INPUT_SYSTEM && DRUMIX_INPUT_SYSTEM
        static bool KeyDown(int i)
        {
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k == null) return false;
            switch (i)
            {
                case 0: return k.aKey.wasPressedThisFrame;
                case 1: return k.sKey.wasPressedThisFrame;
                case 2: return k.dKey.wasPressedThisFrame;
                case 3: return k.fKey.wasPressedThisFrame;
                case 4: return k.gKey.wasPressedThisFrame;
                case 5: return k.hKey.wasPressedThisFrame;
                case 6: return k.jKey.wasPressedThisFrame;
                default: return k.kKey.wasPressedThisFrame;
            }
        }
        static bool EnterDown() { var k = UnityEngine.InputSystem.Keyboard.current; return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame); }
        static bool EscDown() { var k = UnityEngine.InputSystem.Keyboard.current; return k != null && k.escapeKey.wasPressedThisFrame; }
        static bool PauseDown() { var k = UnityEngine.InputSystem.Keyboard.current; return k != null && k.pKey.wasPressedThisFrame; }
#elif ENABLE_LEGACY_INPUT_MANAGER
        static readonly KeyCode[] Keys = { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J, KeyCode.K };
        static bool KeyDown(int i) { return Input.GetKeyDown(Keys[i]); }
        static bool EnterDown() { return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space); }
        static bool EscDown() { return Input.GetKeyDown(KeyCode.Escape); }
        static bool PauseDown() { return Input.GetKeyDown(KeyCode.P); }
#else
        static bool KeyDown(int i) { return false; }
        static bool EnterDown() { return false; }
        static bool EscDown() { return false; }
        static bool PauseDown() { return false; }
#endif
    }
}
