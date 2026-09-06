using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[BepInPlugin("kr.utsutsutsu.animatedtexturefix", "UTSUTSUTSU Korean Animated Texture Fix", "1.0.0")]
public sealed class KoreanAnimatedTextureFix : BaseUnityPlugin
{
    private readonly Dictionary<string, byte[]> pngByName = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    private SpriteRenderer[] renderers = new SpriteRenderer[0];
    private UnityEngine.UI.Image[] images = new UnityEngine.UI.Image[0];
    private TMP_Text[] tmpTexts = new TMP_Text[0];
    private Text[] legacyTexts = new Text[0];
    private TMP_FontAsset galmuriFont;
    private Font galmuriLegacyFont;
    private UIDocument[] uiDocuments = new UIDocument[0];
    private readonly HashSet<int> loggedChoiceObjects = new HashSet<int>();
    private float nextRefresh;
    private int forcedRefreshFrames;

    private void Awake()
    {
        string textureDir = Path.Combine(Path.GetDirectoryName(Info.Location), "KoreanAnimatedTextures");
        if (!Directory.Exists(textureDir))
        {
            Logger.LogError("Texture directory not found: " + textureDir);
            return;
        }
        foreach (string path in Directory.GetFiles(textureDir, "*.png"))
            pngByName[Path.GetFileNameWithoutExtension(path)] = File.ReadAllBytes(path);
        Logger.LogInfo("Loaded " + pngByName.Count + " Korean animation frames.");
        TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loadedFonts.Length; i++)
        {
            if (loadedFonts[i] != null && loadedFonts[i].name.IndexOf("Galmuri", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                galmuriFont = loadedFonts[i];
                break;
            }
        }
        string fontBundlePath = Path.Combine(Path.GetDirectoryName(Info.Location), "Galmuri14FontBundle");
        if (galmuriFont == null && File.Exists(fontBundlePath))
        {
            AssetBundle bundle = AssetBundle.LoadFromFile(fontBundlePath);
            if (bundle != null)
            {
                TMP_FontAsset[] fonts = bundle.LoadAllAssets<TMP_FontAsset>();
                if (fonts.Length > 0)
                {
                    galmuriFont = fonts[0];
                }
            }
        }
        if (galmuriFont != null) Logger.LogInfo("Using Galmuri Regular TMP font: " + galmuriFont.name);
        else Logger.LogWarning("Galmuri TMP font was not found; Yes/No labels will keep their existing font.");
        if (galmuriFont != null) galmuriLegacyFont = galmuriFont.sourceFontFile;
        if (galmuriLegacyFont == null)
        {
            Font[] loadedLegacyFonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int i = 0; i < loadedLegacyFonts.Length; i++)
            {
                if (loadedLegacyFonts[i] != null && loadedLegacyFonts[i].name.IndexOf("Galmuri", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    galmuriLegacyFont = loadedLegacyFonts[i];
                    break;
                }
            }
        }
        if (galmuriLegacyFont == null)
        {
            foreach (AssetBundle loadedBundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                Font[] bundledFonts = loadedBundle.LoadAllAssets<Font>();
                for (int i = 0; i < bundledFonts.Length; i++)
                {
                    if (bundledFonts[i] != null && bundledFonts[i].name.IndexOf("Galmuri", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        galmuriLegacyFont = bundledFonts[i];
                        break;
                    }
                }
                if (galmuriLegacyFont != null) break;
            }
        }
        if (galmuriLegacyFont != null) Logger.LogInfo("Using Galmuri Regular legacy font: " + galmuriLegacyFont.name);
        else Logger.LogWarning("Galmuri legacy Font asset was not found.");
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshTargets();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Newly loaded renderers must be found before their first rendered frame.
        forcedRefreshFrames = 8;
        RefreshTargets();
    }

    private void LateUpdate()
    {
        if (forcedRefreshFrames > 0)
        {
            forcedRefreshFrames--;
            RefreshTargets();
        }
        else if (Time.unscaledTime >= nextRefresh)
            RefreshTargets();
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null) renderer.sprite = ReplacementFor(renderer.sprite);
        }
        for (int i = 0; i < images.Length; i++)
        {
            UnityEngine.UI.Image image = images[i];
            if (image != null) image.sprite = ReplacementFor(image.sprite);
        }
        if (galmuriFont != null)
        {
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                TMP_Text label = tmpTexts[i];
                if (label == null) continue;
                string value = label.text == null ? "" : label.text.Trim();
                if (IsChoiceLabel(label, value))
                {
                    label.font = galmuriFont;
                    LogChoiceOnce(label.GetInstanceID(), "TMP", value, label.font == null ? "null" : label.font.name);
                }
            }
        }
        if (galmuriLegacyFont != null)
        {
            for (int i = 0; i < legacyTexts.Length; i++)
            {
                Text label = legacyTexts[i];
                if (label != null && IsChoiceLabel(label, label.text == null ? "" : label.text.Trim()))
                {
                    label.font = galmuriLegacyFont;
                    LogChoiceOnce(label.GetInstanceID(), "UGUI.Text", label.text, label.font == null ? "null" : label.font.name);
                }
            }
        }
        if (galmuriLegacyFont != null)
        {
            for (int i = 0; i < uiDocuments.Length; i++)
            {
                UIDocument document = uiDocuments[i];
                if (document != null && document.rootVisualElement != null)
                    ApplyChoiceFont(document.rootVisualElement);
            }
        }
    }

    private void RefreshTargets()
    {
        renderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>();
        images = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Image>();
        tmpTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        legacyTexts = Resources.FindObjectsOfTypeAll<Text>();
        uiDocuments = Resources.FindObjectsOfTypeAll<UIDocument>();
        nextRefresh = Time.unscaledTime + 1.0f;
    }

    private void ApplyChoiceFont(VisualElement element)
    {
        TextElement textElement = element as TextElement;
        if (textElement != null && IsChoice(textElement.text == null ? "" : textElement.text.Trim()))
        {
            textElement.style.unityFont = galmuriLegacyFont;
            LogChoiceOnce(textElement.GetHashCode(), "UIElements", textElement.text, galmuriLegacyFont.name);
        }
        for (int i = 0; i < element.hierarchy.childCount; i++)
            ApplyChoiceFont(element.hierarchy[i]);
    }

    private static bool IsChoice(string value)
    {
        return value == "예" || value == "아니요" || value == "はい" || value == "いいえ";
    }

    private static bool IsChoiceLabel(Component component, string value)
    {
        if (IsChoice(value)) return true;
        Transform current = component == null ? null : component.transform;
        for (int depth = 0; current != null && depth < 8; depth++, current = current.parent)
        {
            if (current.name.StartsWith("OptionButton", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void LogChoiceOnce(int id, string framework, string value, string fontName)
    {
        if (loggedChoiceObjects.Add(id))
            Logger.LogInfo("Choice font applied: framework=" + framework + ", text=" + value + ", font=" + fontName);
    }

    private Sprite ReplacementFor(Sprite original)
    {
        if (original == null || original.name.StartsWith("KR$", StringComparison.Ordinal)) return original;
        string key = original.name;
        byte[] png;
        if (!pngByName.TryGetValue(key, out png))
        {
            if (key.EndsWith("_0", StringComparison.Ordinal)) key = key.Substring(0, key.Length - 2);
            if (!pngByName.TryGetValue(key, out png)) return original;
        }
        Sprite replacement;
        string cacheKey = key + "|" + original.pixelsPerUnit.ToString("R");
        if (spriteCache.TryGetValue(cacheKey, out replacement)) return replacement;
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = "KR$" + key;
        if (!ImageConversion.LoadImage(texture, png, false))
        {
            Destroy(texture);
            return original;
        }
        texture.wrapMode = original.texture.wrapMode;
        texture.filterMode = original.texture.filterMode;
        Vector2 pivot = new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height);
        replacement = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot,
            original.pixelsPerUnit, 0, SpriteMeshType.FullRect, original.border);
        replacement.name = "KR$" + key;
        spriteCache[cacheKey] = replacement;
        return replacement;
    }
}
