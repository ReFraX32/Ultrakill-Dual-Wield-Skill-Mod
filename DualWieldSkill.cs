using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace DualWieldSkill
{
    [BepInPlugin("com.ReFraX.ultrakill.dualwieldskill", "Dual Wield Skill", "1.0.0")]
    public class DualWieldMod : BaseUnityPlugin
    {
        private ConfigEntry<float> cooldownConfig;
        private ConfigEntry<string> inputKeyConfig;
        private ConfigEntry<float> powerUpDurationConfig;
        private float nextUseTime = 0f;
        private Text cooldownText;
        private KeyCode inputKey;

        private void Awake()
        {
            // Define the configuration entries
            cooldownConfig = Config.Bind("", "Cooldown Time", 100f, "Cooldown time in seconds for the dual-wield power-up.");
            inputKeyConfig = Config.Bind("", "Activate Dual-Wield", "B", "Key to activate the dual-wield skill.");
            powerUpDurationConfig = Config.Bind("", "Power-Up Duration", 30f, "Duration in seconds for the dual-wield power-up.");

            // Convert the input key to uppercase and parse it
            inputKey = ParseKeyCode(inputKeyConfig.Value);
        }

        private void Start()
        {
            // Create a new GameObject for the UI text
            GameObject uiTextObject = new GameObject("CooldownText");
            uiTextObject.transform.SetParent(GameObject.Find("Canvas").transform);

            // Add a CanvasGroup to make the text non-interactable
            CanvasGroup canvasGroup = uiTextObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // Add a Text component to the GameObject
            cooldownText = uiTextObject.AddComponent<Text>();

            // Configure the Text component
            cooldownText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            cooldownText.fontSize = 24;
            cooldownText.color = Color.white;
            cooldownText.alignment = TextAnchor.UpperLeft;

            // Position the Text component
            RectTransform rectTransform = cooldownText.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.anchoredPosition = new Vector2(10, -10);
        }

        private void Update()
        {
            // Update the input key in case it changes in the configuration
            if (inputKey.ToString() != inputKeyConfig.Value.ToUpper())
            {
                inputKey = ParseKeyCode(inputKeyConfig.Value);
            }

            // Update the cooldown timer
            if (Input.GetKeyDown(inputKey) && Time.time >= nextUseTime)
            {
                ActivateDualWield();
                nextUseTime = Time.time + cooldownConfig.Value;
            }

            // Update the cooldown text
            float remainingTime = Mathf.Max(0, nextUseTime - Time.time);
            if (remainingTime > 0)
            {
                cooldownText.text = $"Dual Wield: {remainingTime:F1}s";
            }
            else
            {
                cooldownText.text = "Dual Wield: Ready!";
            }
        }

        private void ActivateDualWield()
        {
            // Mimic the logic in DualWieldPickup.PickedUp
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
            dualWield.juiceAmount = powerUpDurationConfig.Value;  // Dual wield duration
            if (componentsInChildren != null && componentsInChildren.Length != 0)
            {
                dualWield.delay += (float)componentsInChildren.Length / 20f;
            }
        }

        private KeyCode ParseKeyCode(string key)
        {
            // Convert the key string to uppercase and parse it
            return (KeyCode)System.Enum.Parse(typeof(KeyCode), key.ToUpper());
        }
    }
}
