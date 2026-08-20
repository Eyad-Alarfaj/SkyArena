using SkyArena.Inputs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SkyArena.EditorTools
{
    /// <summary>
    /// Every reference the generated Canvas hands back to
    /// <see cref="SkyArenaBuilder"/> so it can wire up the scene components.
    /// </summary>
    internal class HudReferences
    {
        public Canvas Canvas;
        public RectTransform CanvasRect;

        public VirtualJoystick Joystick;
        public HoldButton ThrottleUp;
        public HoldButton ThrottleDown;
        public HoldButton Gun;
        public HoldButton Missile;

        public Image HealthFill;
        public Image ThrottleFill;
        public Image MissileCooldown;
        public Image LockProgressRing;

        public Text HealthText;
        public Text SpeedText;
        public Text AltitudeText;
        public Text StatusText;
        public Text WeaponStatusText;

        public RectTransform Reticle;
        public Image[] ReticleGraphics;

        public RectTransform RadarPanel;
        public RectTransform BlipTemplate;

        public GameObject DeathOverlay;
    }

    /// <summary>
    /// Builds the entire touch HUD from code so the prototype needs no
    /// hand-authored Canvas. Laid out against a 1920x1080 reference and scaled
    /// by CanvasScaler, so it holds up on any phone aspect ratio.
    /// </summary>
    internal static class SkyArenaUiFactory
    {
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private static readonly Color Accent = new Color(0.35f, 0.85f, 1f, 1f);
        private static readonly Color Panel = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color ButtonIdle = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color ButtonPressed = new Color(0.35f, 0.85f, 1f, 0.6f);
        private static readonly Color TextColor = new Color(0.92f, 0.96f, 1f, 1f);

        private static Font uiFont;
        private static Sprite discSprite;
        private static Sprite boxSprite;

        public static HudReferences Build()
        {
            CacheBuiltinAssets();

            HudReferences refs = new HudReferences();

            GameObject canvasObject = new GameObject("HUD Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            refs.Canvas = canvas;
            refs.CanvasRect = canvasObject.GetComponent<RectTransform>();

            Transform root = canvasObject.transform;

            BuildStatusText(root, refs);
            BuildHealthBar(root, refs);
            BuildFlightReadouts(root, refs);
            BuildRadar(root, refs);
            BuildCrosshair(root);
            BuildWeaponStatus(root, refs);
            BuildLockReticle(root, refs);
            BuildJoystick(root, refs);
            BuildThrottleGauge(root, refs);
            BuildActionButtons(root, refs);
            BuildHintText(root);
            BuildDeathOverlay(root, refs);

            CreateEventSystem();

            return refs;
        }

        private static void CacheBuiltinAssets()
        {
            uiFont = LoadFont();
            discSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            boxSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static Font LoadFont()
        {
            // Unity 2022.2+ renamed the built-in font; fall back for safety.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null) Debug.LogWarning("[SkyArena] No built-in UI font found; HUD labels may be blank.");
            return font;
        }

        private static void CreateEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));

            // This project ships with Active Input Handling set to the new Input
            // System, which needs its own UI module; the legacy module would
            // silently receive no pointer events at all.
#if ENABLE_INPUT_SYSTEM
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystem.AddComponent<StandaloneInputModule>();
#endif
        }

        // ---------------------------------------------------------------- HUD

        private static void BuildStatusText(Transform parent, HudReferences refs)
        {
            Text status = CreateText("StatusText", parent, "Connecting...", 30, TextAnchor.UpperCenter);
            SetRect(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -26f), new Vector2(1000f, 40f));
            status.color = Accent;
            refs.StatusText = status;
        }

        private static void BuildHealthBar(Transform parent, HudReferences refs)
        {
            RectTransform bar = CreatePanel("HealthBar", parent, Panel).rectTransform;
            SetRect(bar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -40f), new Vector2(440f, 36f));

            Image fill = CreateImage("Fill", bar, boxSprite, new Color(0.25f, 0.85f, 0.45f));
            Stretch(fill.rectTransform, 3f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            Text label = CreateText("HealthText", bar, "100", 24, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 0f);

            Text caption = CreateText("Caption", bar, "HULL", 18, TextAnchor.MiddleLeft);
            SetRect(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f),
                new Vector2(2f, 4f), new Vector2(120f, 24f));
            caption.color = Accent;

            refs.HealthFill = fill;
            refs.HealthText = label;
        }

        private static void BuildFlightReadouts(Transform parent, HudReferences refs)
        {
            Text speed = CreateText("SpeedText", parent, "0 km/h", 30, TextAnchor.MiddleLeft);
            SetRect(speed.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -88f), new Vector2(380f, 38f));

            Text altitude = CreateText("AltitudeText", parent, "ALT 0 m", 26, TextAnchor.MiddleLeft);
            SetRect(altitude.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -128f), new Vector2(380f, 34f));
            altitude.color = new Color(0.75f, 0.85f, 0.95f);

            refs.SpeedText = speed;
            refs.AltitudeText = altitude;
        }

        private static void BuildRadar(Transform parent, HudReferences refs)
        {
            Image panel = CreateImage("Radar", parent, discSprite, new Color(0.05f, 0.12f, 0.1f, 0.55f));
            RectTransform panelRect = panel.rectTransform;
            SetRect(panelRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-170f, -170f), new Vector2(280f, 280f));

            Color grid = new Color(0.4f, 1f, 0.7f, 0.18f);
            CreateDecorBar("GridH", panelRect, new Vector2(240f, 2f), Vector2.zero, grid);
            CreateDecorBar("GridV", panelRect, new Vector2(2f, 240f), Vector2.zero, grid);

            // The player is always dead centre, pointing up.
            CreateDecorBar("Self", panelRect, new Vector2(8f, 16f), Vector2.zero, new Color(0.4f, 1f, 0.7f, 0.95f));

            Image blip = CreateImage("BlipTemplate", panelRect, discSprite, new Color(1f, 0.3f, 0.3f));
            SetRect(blip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(18f, 18f));
            blip.gameObject.SetActive(false);

            refs.RadarPanel = panelRect;
            refs.BlipTemplate = blip.rectTransform;
        }

        private static void BuildCrosshair(Transform parent)
        {
            RectTransform root = CreateEmpty("Crosshair", parent);
            SetRect(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(64f, 64f));

            Color color = new Color(1f, 1f, 1f, 0.7f);
            CreateDecorBar("Left", root, new Vector2(22f, 3f), new Vector2(-26f, 0f), color);
            CreateDecorBar("Right", root, new Vector2(22f, 3f), new Vector2(26f, 0f), color);
            CreateDecorBar("Top", root, new Vector2(3f, 22f), new Vector2(0f, 26f), color);
            CreateDecorBar("Bottom", root, new Vector2(3f, 22f), new Vector2(0f, -26f), color);
            CreateDecorBar("Dot", root, new Vector2(4f, 4f), Vector2.zero, color);
        }

        private static void BuildWeaponStatus(Transform parent, HudReferences refs)
        {
            // Sits just under the crosshair, where the player is already
            // looking during a fight.
            Text status = CreateText("WeaponStatusText", parent, string.Empty, 28, TextAnchor.MiddleCenter);
            SetRect(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -96f), new Vector2(560f, 40f));
            refs.WeaponStatusText = status;
        }

        private static void BuildLockReticle(Transform parent, HudReferences refs)
        {
            RectTransform reticle = CreateEmpty("LockReticle", parent);
            SetRect(reticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(120f, 120f));

            Image ring = CreateImage("LockProgress", reticle, discSprite, new Color(1f, 0.78f, 0.2f, 0.28f));
            Stretch(ring.rectTransform, 18f);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0f;

            // Four corner brackets, so the reticle frames the target instead of
            // covering it.
            Image[] brackets = new Image[8];
            int index = 0;
            Color bracketColor = new Color(1f, 0.78f, 0.2f);

            for (int cornerX = -1; cornerX <= 1; cornerX += 2)
            {
                for (int cornerY = -1; cornerY <= 1; cornerY += 2)
                {
                    brackets[index++] = CreateDecorBar(
                        $"BracketH_{cornerX}_{cornerY}", reticle,
                        new Vector2(36f, 4f), new Vector2(cornerX * 42f, cornerY * 58f), bracketColor);

                    brackets[index++] = CreateDecorBar(
                        $"BracketV_{cornerX}_{cornerY}", reticle,
                        new Vector2(4f, 36f), new Vector2(cornerX * 58f, cornerY * 42f), bracketColor);
                }
            }

            reticle.gameObject.SetActive(false);

            refs.Reticle = reticle;
            refs.ReticleGraphics = brackets;
            refs.LockProgressRing = ring;
        }

        // ------------------------------------------------------------ Controls

        private static void BuildJoystick(Transform parent, HudReferences refs)
        {
            Image background = CreateImage("FlightJoystick", parent, discSprite, new Color(1f, 1f, 1f, 0.16f));
            background.raycastTarget = true;
            RectTransform backgroundRect = background.rectTransform;
            SetRect(backgroundRect, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f),
                new Vector2(270f, 270f), new Vector2(320f, 320f));

            Image handle = CreateImage("Handle", backgroundRect, discSprite, new Color(0.35f, 0.85f, 1f, 0.55f));
            SetRect(handle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(130f, 130f));

            VirtualJoystick joystick = background.gameObject.AddComponent<VirtualJoystick>();
            SkyArenaBuilder.WireField(joystick, "background", backgroundRect);
            SkyArenaBuilder.WireField(joystick, "handle", handle.rectTransform);

            Text label = CreateText("Label", backgroundRect, "PITCH / ROLL", 18, TextAnchor.MiddleCenter);
            SetRect(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(320f, 26f));
            label.color = new Color(1f, 1f, 1f, 0.5f);

            refs.Joystick = joystick;
        }

        private static void BuildThrottleGauge(Transform parent, HudReferences refs)
        {
            Image track = CreateImage("ThrottleGauge", parent, boxSprite, Panel);
            SetRect(track.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(-560f, 262f), new Vector2(28f, 260f));

            Image fill = CreateImage("Fill", track.rectTransform, boxSprite, Accent);
            Stretch(fill.rectTransform, 3f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 0.3f;

            refs.ThrottleFill = fill;
        }

        private static void BuildActionButtons(Transform parent, HudReferences refs)
        {
            refs.Gun = CreateHoldButton("GunButton", parent, "GUN",
                new Vector2(-190f, 190f), new Vector2(200f, 200f), out _);

            refs.Missile = CreateHoldButton("MissileButton", parent, "MSL",
                new Vector2(-190f, 415f), new Vector2(190f, 190f), out RectTransform missileRect);

            refs.ThrottleUp = CreateHoldButton("ThrottleUpButton", parent, "+",
                new Vector2(-420f, 350f), new Vector2(150f, 150f), out _);

            refs.ThrottleDown = CreateHoldButton("ThrottleDownButton", parent, "-",
                new Vector2(-420f, 175f), new Vector2(150f, 150f), out _);

            // Cooldown veil that sweeps off the missile button as it reloads.
            Image cooldown = CreateImage("Cooldown", missileRect, discSprite, new Color(0f, 0f, 0f, 0.6f));
            Stretch(cooldown.rectTransform, 0f);
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = (int)Image.Origin360.Top;
            cooldown.fillClockwise = true;
            cooldown.fillAmount = 0f;

            refs.MissileCooldown = cooldown;
        }

        private static HoldButton CreateHoldButton(
            string name, Transform parent, string label, Vector2 position, Vector2 size, out RectTransform rect)
        {
            Image image = CreateImage(name, parent, discSprite, ButtonIdle);
            image.raycastTarget = true;
            rect = image.rectTransform;
            SetRect(rect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), position, size);

            Text text = CreateText("Label", rect, label, 34, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 0f);

            HoldButton button = image.gameObject.AddComponent<HoldButton>();
            SkyArenaBuilder.WireField(button, "tintTarget", image);
            SkyArenaBuilder.WireColor(button, "pressedTint", ButtonPressed);
            return button;
        }

        private static void BuildHintText(Transform parent)
        {
            Text hint = CreateText("Hints", parent,
                "Drag the stick to fly  -  + / - throttle  -  GUN fires  -  hold MSL when the reticle turns red",
                20, TextAnchor.LowerCenter);
            SetRect(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 22f), new Vector2(1400f, 32f));
            hint.color = new Color(1f, 1f, 1f, 0.4f);
        }

        private static void BuildDeathOverlay(Transform parent, HudReferences refs)
        {
            Image overlay = CreateImage("DeathOverlay", parent, boxSprite, new Color(0.4f, 0f, 0f, 0.45f));
            Stretch(overlay.rectTransform, 0f);
            overlay.raycastTarget = false;

            Text text = CreateText("Text", overlay.rectTransform, "DESTROYED\nrespawning...", 60, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 0f);

            overlay.gameObject.SetActive(false);
            refs.DeathOverlay = overlay.gameObject;
        }

        // ------------------------------------------------------------ Helpers

        private static RectTransform CreateEmpty(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = CreateEmpty(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreatePanel(string name, Transform parent, Color color)
        {
            return CreateImage(name, parent, boxSprite, color);
        }

        private static Image CreateDecorBar(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            Image bar = CreateImage(name, parent, boxSprite, color);
            SetRect(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position, size);
            return bar;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor anchor)
        {
            RectTransform rect = CreateEmpty(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = uiFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = TextColor;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetRect(
            RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
