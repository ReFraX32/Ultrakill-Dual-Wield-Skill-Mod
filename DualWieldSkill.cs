using BepInEx;
using BepInEx.Configuration;
using Configgy;
using HarmonyLib;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DualWieldSkill
{
    [BepInPlugin("com.ReFraX.ultrakill.dualwieldskill", "Dual Wield Skill", "2.1.0")]
    public class DualWieldMod : BaseUnityPlugin
    {
        public static DualWieldMod Instance;
        private ConfigEntry<float> cooldownConfig;
        private ConfigEntry<float> powerUpDurationConfig;

        [Configgable("UI", "Horizontal Position")]
        private static FloatSlider uiXPositionConfig = new FloatSlider(-750, -1000, 1000);

        [Configgable("UI", "Vertical Position")]
        private static FloatSlider uiYPositionConfig = new FloatSlider(400, -600, 600);

        [Configgable("UI", "Icon Size")]
        private static FloatSlider uiScaleConfig = new FloatSlider(1.0f, 0.5f, 2.0f);

        [Configgable("", "Activate Dual-Wield", 0, null)]
        public static ConfigKeybind inputKeyConfig = new ConfigKeybind((KeyCode)98);

        private float nextUseTime = 0f;
        private float sceneLoadTime = 0f;
        private bool uiFadeInStarted = false;
        private Image activeIcon;
        private Image inactiveIcon;
        private Text timerText;
        private Font customFont;
        private Canvas uiCanvas;
        private CanvasGroup canvasGroup;
        private bool inputEnabled = true;
        private bool wasDead = false;
        private GameObject uiContainer;
        private float lastXValue = 0f;
        private float lastYValue = 0f;
        private float lastScaleValue = 1.0f;

        // Store the original cooldown value when it changes during active cooldown
        private float originalCooldownValue;
        private float cooldownStartTime;
        private bool cooldownActive = false;

        private void Awake()
        {
            Instance = this;
            cooldownConfig = Config.Bind("Gameplay", "Cooldown Time", 120f, "Cooldown time in seconds for the dual-wield power-up.");
            powerUpDurationConfig = Config.Bind("Gameplay", "Power-Up Duration", 30f, "Duration in seconds for the dual-wield power-up.");

            // Store the initial cooldown value
            originalCooldownValue = cooldownConfig.Value;

            // Add a callback for when the cooldown value changes
            cooldownConfig.SettingChanged += OnCooldownSettingChanged;

            ConfigBuilder configBuilder = new ConfigBuilder("Dual Wield Skill", null);
            configBuilder.BuildAll();

            customFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var harmony = new Harmony("com.ReFraX.ultrakill.dualwieldskill");
            harmony.PatchAll();

            CheckBepInExConfig();
        }

        private void OnCooldownSettingChanged(object sender, System.EventArgs e)
        {
            // If cooldown is active, recalculate based on elapsed time and new total
            if (cooldownActive)
            {
                float elapsedTime = Time.time - cooldownStartTime;
                float remainingTimePercentage = (originalCooldownValue - elapsedTime) / originalCooldownValue;
                // Apply the same percentage to the new cooldown value
                float newRemainingTime = remainingTimePercentage * cooldownConfig.Value;
                nextUseTime = Time.time + newRemainingTime;
                // Update the original values for future calculations
                originalCooldownValue = cooldownConfig.Value;
                cooldownStartTime = Time.time - (cooldownConfig.Value - newRemainingTime);
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void CheckBepInExConfig()
        {
            string configPath = Path.Combine(Paths.ConfigPath, "BepInEx.cfg");
            if (File.Exists(configPath) && File.ReadAllText(configPath).Contains("HideManagerGameObject = false"))
            {
                Logger.LogWarning("For the Dual Wield Skill mod to function correctly with newer ULTRAKILL versions, please set 'HideManagerGameObject = true' in BepInEx.cfg.");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Reset cooldown regardless of whether it's a restart or new level load
            ResetCooldown();

            sceneLoadTime = Time.time;
            uiFadeInStarted = false;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            UpdateUIPosition();
            Invoke("EnsureUIVisible", 3.0f);
        }

        private void EnsureUIVisible()
        {
            if (!uiFadeInStarted && !IsRealMenu() && !IsPauseMenu())
            {
                uiFadeInStarted = true;
                uiCanvas.enabled = true;
                canvasGroup.alpha = 1f;
            }
        }

        private void CreateUICanvas()
        {
            // Destroy existing canvas if it exists
            GameObject existingCanvas = GameObject.Find("DualWieldCanvas");
            if (existingCanvas != null)
            {
                Destroy(existingCanvas);
            }

            // Create a new canvas
            GameObject canvasObj = new GameObject("DualWieldCanvas");
            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGroup = canvasObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);
        }

        private void Start()
        {
            CreateUICanvas();

            GameObject cooldownObject = new GameObject("CooldownUI");
            cooldownObject.transform.SetParent(uiCanvas.transform, false);

            try
            {
                Sprite activeSprite = LoadEmbeddedResource("Dual_Wield_Skill.Assets.Dual Wield Skill Icon.png");
                Sprite inactiveSprite = LoadEmbeddedResource("Dual_Wield_Skill.Assets.Dual Wield Skill Icon Inactive.png");

                uiContainer = new GameObject("UIContainer");
                uiContainer.transform.SetParent(cooldownObject.transform, false);

                RectTransform containerRect = uiContainer.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0.5f, 0.5f);
                containerRect.anchorMax = new Vector2(0.5f, 0.5f);
                containerRect.pivot = new Vector2(0.5f, 0.5f);
                containerRect.sizeDelta = new Vector2(150, 150);

                GameObject inactiveIconObj = new GameObject("InactiveIcon");
                inactiveIconObj.transform.SetParent(uiContainer.transform, false);
                inactiveIcon = inactiveIconObj.AddComponent<Image>();
                inactiveIcon.sprite = inactiveSprite;
                inactiveIcon.type = Image.Type.Simple;
                inactiveIcon.preserveAspect = true;

                GameObject activeIconObj = new GameObject("ActiveIcon");
                activeIconObj.transform.SetParent(uiContainer.transform, false);
                activeIcon = activeIconObj.AddComponent<Image>();
                activeIcon.sprite = activeSprite;
                activeIcon.type = Image.Type.Filled;
                activeIcon.fillMethod = Image.FillMethod.Radial360;
                activeIcon.fillOrigin = 2;
                activeIcon.fillClockwise = true;
                activeIcon.preserveAspect = true;

                foreach (RectTransform rect in new[] { inactiveIcon.GetComponent<RectTransform>(), activeIcon.GetComponent<RectTransform>() })
                {
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(100, 100);
                }

                GameObject timerObject = new GameObject("TimerText");
                timerObject.transform.SetParent(uiContainer.transform, false);
                timerText = timerObject.AddComponent<Text>();
                timerText.font = customFont;
                timerText.fontSize = 24;
                timerText.color = Color.yellow;
                timerText.alignment = TextAnchor.MiddleCenter;

                RectTransform timerRect = timerText.GetComponent<RectTransform>();
                timerRect.anchorMin = new Vector2(0.5f, 0.5f);
                timerRect.anchorMax = new Vector2(0.5f, 0.5f);
                timerRect.pivot = new Vector2(0.5f, 0.5f);
                timerRect.anchoredPosition = Vector2.zero;
                timerRect.sizeDelta = new Vector2(150, 50);

                // Make sure text is on top of icons
                timerRect.SetAsLastSibling();

                UpdateUIPosition();
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error setting up UI: " + e);
            }
        }

        private void UpdateUIPosition()
        {
            if (uiContainer == null || uiCanvas == null)
                return;

            RectTransform containerRect = uiContainer.GetComponent<RectTransform>();
            if (containerRect == null)
                return;

            float xPos = uiXPositionConfig.Value;
            float yPos = uiYPositionConfig.Value;
            float scale = uiScaleConfig.Value;

            containerRect.anchoredPosition = new Vector2(xPos, yPos);
            containerRect.localScale = new Vector3(scale, scale, 1f);

            lastXValue = xPos;
            lastYValue = yPos;
            lastScaleValue = scale;
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
                ImageConversion.LoadImage(texture, buffer);

                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }

        private void Update()
        {
            if (!activeIcon || !inactiveIcon || !timerText || !canvasGroup) return;

            if (Time.time - sceneLoadTime < 2.5f)
            {
                uiCanvas.enabled = false;
                return;
            }

            // Start fade-in after delay
            if (!uiFadeInStarted)
            {
                uiFadeInStarted = true;
                canvasGroup.alpha = 0f;
                uiCanvas.enabled = true;
            }

            if (canvasGroup.alpha < 1f)
            {
                canvasGroup.alpha = Mathf.Min(1f, canvasGroup.alpha + Time.deltaTime * 2f);
            }

            if (uiXPositionConfig.Value != lastXValue ||
                uiYPositionConfig.Value != lastYValue ||
                uiScaleConfig.Value != lastScaleValue)
            {
                UpdateUIPosition();
            }

            bool isDead = NewMovement.Instance != null && NewMovement.Instance.dead;
            if (isDead)
            {
                SetUIVisibility(false);
                wasDead = true;
                return;
            }
            else if (wasDead)
            {
                wasDead = false;
                uiCanvas.enabled = true;
                canvasGroup.alpha = 1f;
                UpdateUIPosition();
            }

            if (IsRealMenu())
            {
                SetUIVisibility(false);
                inputEnabled = false;
                return;
            }

            inputEnabled = !IsPauseMenu();

            if (!uiFadeInStarted && Time.time - sceneLoadTime >= 2.5f)
            {
                uiFadeInStarted = true;
                uiCanvas.enabled = true;
            }

            if (uiFadeInStarted && canvasGroup.alpha < 1f)
            {
                canvasGroup.alpha = Mathf.Min(1f, canvasGroup.alpha + Time.deltaTime * 2f);
            }

            if (Time.time - sceneLoadTime < 2.5f)
                return;

            if (inputEnabled && Input.GetKeyDown(((ConfigValueElement<KeyCode>)(object)inputKeyConfig).Value) && Time.time >= nextUseTime)
            {
                ActivateDualWield();
                nextUseTime = Time.time + cooldownConfig.Value;
                cooldownActive = true;
                cooldownStartTime = Time.time;
                originalCooldownValue = cooldownConfig.Value;
            }

            float remainingTime = Mathf.Max(0, nextUseTime - Time.time);
            if (remainingTime > 0)
            {
                // Calculate fill amount based on current cooldown value, not the original
                float currentCooldownTotal = cooldownActive ? originalCooldownValue : cooldownConfig.Value;
                float elapsedTime = currentCooldownTotal - remainingTime;
                activeIcon.fillAmount = elapsedTime / currentCooldownTotal;
                inactiveIcon.gameObject.SetActive(true);
                timerText.text = $"{remainingTime:F1}s";
            }
            else
            {
                activeIcon.fillAmount = 1;
                inactiveIcon.gameObject.SetActive(false);
                timerText.text = "Ready!";
                cooldownActive = false;
            }

            if (!cooldownActive)
            {
                activeIcon.fillAmount = 1;
                inactiveIcon.gameObject.SetActive(false);
                timerText.text = "Ready!";
                return;
            }

        }

        private void SetUIVisibility(bool visible)
        {
            if (uiCanvas != null)
                uiCanvas.enabled = visible;
        }

        public static bool IsRealMenu()
        {
            if (SceneHelper.CurrentScene == null)
                return true;

            string[] realMenus = { "Intro", "Bootstrap", "Main Menu", "Level 2-S", "Intermission1", "Intermission2" };
            return realMenus.Contains(SceneHelper.CurrentScene);
        }

        public static bool IsPauseMenu()
        {
            return MonoSingleton<OptionsManager>.Instance != null &&
                   MonoSingleton<OptionsManager>.Instance.paused &&
                   !IsRealMenu();
        }

        public void ResetCooldown()
        {
            nextUseTime = 0f;
            cooldownActive = false;

            if (activeIcon != null)
                activeIcon.fillAmount = 1f;

            if (inactiveIcon != null)
                inactiveIcon.gameObject.SetActive(false);

            if (timerText != null)
                timerText.text = "Ready!";
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

    [HarmonyPatch(typeof(OptionsMenuToManager), "RestartCheckpoint")]
    public static class CheckpointRestartPatch
    {
        [HarmonyPrefix]
        public static void OnCheckpointRestart()
        {
            if (DualWieldMod.Instance != null)
            {
                DualWieldMod.Instance.ResetCooldown();
            }
        }
    }

    [HarmonyPatch(typeof(NewMovement), "GetHurt")]
    public static class PlayerDeathPatch
    {
        [HarmonyPostfix]
        public static void OnPlayerDeath()
        {
            if (NewMovement.Instance != null && NewMovement.Instance.dead)
            {
                if (DualWieldMod.Instance != null)
                {
                    DualWieldMod.Instance.ResetCooldown();
                }
            }
        }
    }
}