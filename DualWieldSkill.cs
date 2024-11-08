using BepInEx;
using BepInEx.Configuration;
using Configgy;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace DualWieldSkill
{
    [BepInPlugin("com.ReFraX.ultrakill.dualwieldskill", "Dual Wield Skill", "2.0.0")]
    public class DualWieldMod : BaseUnityPlugin
    {
        private ConfigEntry<float> cooldownConfig;
        private ConfigEntry<float> powerUpDurationConfig;
        [Configgable("", "Activate Dual-Wield", 0, null)]
        public static ConfigKeybind inputKeyConfig = new ConfigKeybind((KeyCode)98);

        private float nextUseTime = 0f;
        private Image activeIcon;
        private Image inactiveIcon;
        private Text cooldownText;
        private Text timerText;
        private Font customFont;
        private Canvas uiCanvas;

        private void Awake()
        {
            cooldownConfig = Config.Bind("", "Cooldown Time", 120f, "Cooldown time in seconds for the dual-wield power-up.");
            powerUpDurationConfig = Config.Bind("", "Power-Up Duration", 30f, "Duration in seconds for the dual-wield power-up.");

            ConfigBuilder configBuilder = new ConfigBuilder("Dual Wield Skill", (string)null);
            configBuilder.BuildAll();

            customFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void CreateUICanvas()
        {
            // Create main canvas
            GameObject canvasObj = new GameObject("DualWieldCanvas");
            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            // Add scaling component
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            // Add raycaster for UI interactions if needed
            canvasObj.AddComponent<GraphicRaycaster>();

            // Make sure the canvas persists between scenes if needed
            DontDestroyOnLoad(canvasObj);
        }


        private void Start()
        {
            CreateUICanvas();

            GameObject cooldownObject = new GameObject("CooldownUI");
            cooldownObject.transform.SetParent(uiCanvas.transform, false);

            try
            {
                // Load both versions of the icon
                Sprite activeSprite = LoadEmbeddedResource("Dual_Wield_Skill.Assets.Dual Wield Skill Icon.png");
                Sprite inactiveSprite = LoadEmbeddedResource("Dual_Wield_Skill.Assets.Dual Wield Skill Icon Inactive.png");

                // Create UI container for better organization and scaling
                GameObject uiContainer = new GameObject("UIContainer");
                uiContainer.transform.SetParent(cooldownObject.transform, false);
                RectTransform containerRect = uiContainer.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0, 1);
                containerRect.anchorMax = new Vector2(0, 1);
                containerRect.pivot = new Vector2(0, 1);
                containerRect.anchoredPosition = new Vector2(-900, 550);
                containerRect.sizeDelta = new Vector2(150, 150);

                // Create the inactive (grey) icon
                GameObject inactiveIconObj = new GameObject("InactiveIcon");
                inactiveIconObj.transform.SetParent(uiContainer.transform, false);
                inactiveIcon = inactiveIconObj.AddComponent<Image>();
                inactiveIcon.sprite = inactiveSprite;
                inactiveIcon.type = Image.Type.Simple;
                inactiveIcon.preserveAspect = true;

                // Create the active (colored) icon with radial fill
                GameObject activeIconObj = new GameObject("ActiveIcon");
                activeIconObj.transform.SetParent(uiContainer.transform, false);
                activeIcon = activeIconObj.AddComponent<Image>();
                activeIcon.sprite = activeSprite;
                activeIcon.type = Image.Type.Filled;
                activeIcon.fillMethod = Image.FillMethod.Radial360;
                activeIcon.fillOrigin = 2;
                activeIcon.fillClockwise = true;
                activeIcon.preserveAspect = true;

                // Set up icon RectTransforms
                foreach (RectTransform rect in new[] { inactiveIcon.GetComponent<RectTransform>(), activeIcon.GetComponent<RectTransform>() })
                {
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 1);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(100, 100);
                }

                // Create the skill name text
                GameObject textObject = new GameObject("SkillText");
                textObject.transform.SetParent(uiContainer.transform, false);
                cooldownText = textObject.AddComponent<Text>();
                cooldownText.font = customFont;
                cooldownText.fontSize = 18;
                cooldownText.color = Color.white;
                cooldownText.alignment = TextAnchor.UpperLeft;
                cooldownText.text = "Dual Wield Skill";

                RectTransform textRect = cooldownText.GetComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0, 1);
                textRect.anchorMax = new Vector2(0, 1);
                textRect.pivot = new Vector2(0, 1);
                textRect.anchoredPosition = new Vector2(0, -105);
                textRect.sizeDelta = new Vector2(150, 30);

                // Create the timer text
                GameObject timerObject = new GameObject("TimerText");
                timerObject.transform.SetParent(uiContainer.transform, false);
                timerText = timerObject.AddComponent<Text>();
                timerText.font = customFont;
                timerText.fontSize = 24;
                timerText.color = Color.yellow;
                timerText.alignment = TextAnchor.MiddleCenter;

                RectTransform timerRect = timerText.GetComponent<RectTransform>();
                timerRect.anchorMin = new Vector2(0, 1);
                timerRect.anchorMax = new Vector2(0, 1);
                timerRect.pivot = new Vector2(0, 1);
                timerRect.anchoredPosition = new Vector2(0, -40);
                timerRect.sizeDelta = new Vector2(100, 30);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error setting up UI: " + e.ToString());
            }
        }

        private Sprite LoadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Debug.LogError($"Resource not found: {resourceName}");
                    return null;
                }

                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(buffer);
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }


        private void Update()
        {
            if (!activeIcon || !inactiveIcon || !timerText) return;

            if (Input.GetKeyDown(((ConfigValueElement<KeyCode>)(object)inputKeyConfig).Value) && Time.time >= nextUseTime)
            {
                ActivateDualWield();
                nextUseTime = Time.time + cooldownConfig.Value;
            }

            float remainingTime = Mathf.Max(0, nextUseTime - Time.time);
            if (remainingTime > 0)
            {
                activeIcon.fillAmount = (cooldownConfig.Value - remainingTime) / cooldownConfig.Value;
                inactiveIcon.gameObject.SetActive(true);
                timerText.text = $"{remainingTime:F1}s";
            }
            else
            {
                activeIcon.fillAmount = 1;
                inactiveIcon.gameObject.SetActive(false);
                timerText.text = "Ready!";
            }
        }

        private void ActivateDualWield()
        {
            if (!MonoSingleton<GunControl>.Instance) return;

            GameObject val = new GameObject();
            val.transform.SetParent(MonoSingleton<GunControl>.Instance.transform, true);
            val.transform.localRotation = Quaternion.identity;

            DualWield[] componentsInChildren = MonoSingleton<GunControl>.Instance.GetComponentsInChildren<DualWield>();
            if (componentsInChildren != null && componentsInChildren.Length % 2 == 0)
            {
                val.transform.localScale = new Vector3(-1f, 1f, 1f);
            }
            else
            {
                val.transform.localScale = Vector3.one;
            }
            if (componentsInChildren == null || componentsInChildren.Length == 0)
            {
                val.transform.localPosition = Vector3.zero;
            }
            else if (componentsInChildren.Length % 2 == 0)
            {
                val.transform.localPosition = new Vector3((float)(componentsInChildren.Length / 2) * -1.5f, 0f, 0f);
            }
            else
            {
                val.transform.localPosition = new Vector3((float)((componentsInChildren.Length + 1) / 2) * 1.5f, 0f, 0f);
            }

            DualWield dualWield = val.AddComponent<DualWield>();
            dualWield.delay = 0.05f;
            dualWield.juiceAmount = powerUpDurationConfig.Value;
            if (componentsInChildren != null && componentsInChildren.Length != 0)
            {
                dualWield.delay += (float)componentsInChildren.Length / 20f;
            }
        }
    }
}