using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using UnityEngine;

[BepInPlugin("sawet.stonewards.xray", "Stonewards X-Ray by sawet", "1.1.0")]
[DefaultExecutionOrder(10000)]
public class StonewardsXRayPlugin : BaseUnityPlugin
{
    private enum TargetMode { Chests, Discoverables, Both }
    private enum TargetGroup { Chest, Discoverable }

    private sealed class TerrainState
    {
        public Terrain Terrain;
        public bool Heightmap;
        public bool Foliage;
    }

    private sealed class RendererState
    {
        public Renderer Renderer;
        public bool Enabled;
    }

    private sealed class TargetRendererState
    {
        public Renderer Renderer;
        public bool Enabled;
        public Material[] Materials;
    }

    private sealed class DiggerState
    {
        public Component System;
        public int ChildCount;
    }

    private sealed class Target
    {
        public Transform Transform;
        public Renderer[] Renderers;
        public string Category;
        public bool Highlighted;
    }

    private sealed class TargetType
    {
        public Type Type;
        public TargetGroup Group;
        public string Category;
    }

    private const int UiXRay = 0;
    private const int UiTerrain = 1;
    private const int UiTarget = 2;
    private const int UiDistance = 3;
    private const int UiOpacity = 4;
    private const int UiOverlay = 5;
    private const int UiChests = 6;
    private const int UiDiscoverables = 7;
    private const int UiBoth = 8;
    private const int UiOn = 9;
    private const int UiOff = 10;

    private static readonly Dictionary<string, string[]> Ui =
        new Dictionary<string, string[]>
        {
            { "en", new[] { "X-Ray", "Terrain", "Target", "Distance", "Opacity", "Overlay", "Chests", "Discoverables", "Chests + Discoverables", "ON", "OFF" } },
            { "fr", new[] { "Rayons X", "Terrain", "Cible", "Distance", "Opacité", "Interface", "Coffres", "Objets à découvrir", "Coffres + objets à découvrir", "ON", "OFF" } },
            { "de", new[] { "Röntgen", "Terrain", "Ziel", "Distanz", "Deckkraft", "Overlay", "Truhen", "Fundstücke", "Truhen + Fundstücke", "AN", "AUS" } },
            { "es", new[] { "Rayos X", "Terreno", "Objetivo", "Distancia", "Opacidad", "Interfaz", "Cofres", "Descubribles", "Cofres + descubribles", "ON", "OFF" } },
            { "zh-hans", new[] { "透视", "地形", "目标", "距离", "不透明度", "界面", "宝箱", "可发现物品", "宝箱 + 可发现物品", "开", "关" } },
            { "zh-hant", new[] { "透視", "地形", "目標", "距離", "不透明度", "介面", "寶箱", "可發現物品", "寶箱 + 可發現物品", "開", "關" } },
            { "ja", new[] { "透視", "地形", "対象", "距離", "不透明度", "オーバーレイ", "宝箱", "発見物", "宝箱 + 発見物", "ON", "OFF" } },
            { "pt", new[] { "Raio-X", "Terreno", "Alvo", "Distância", "Opacidade", "Interface", "Baús", "Descobertas", "Baús + descobertas", "ON", "OFF" } },
            { "ru", new[] { "Рентген", "Рельеф", "Цель", "Дистанция", "Непрозрачность", "Интерфейс", "Сундуки", "Находки", "Сундуки + находки", "ВКЛ", "ВЫКЛ" } }
        };

    private readonly Dictionary<int, TerrainState> _terrains =
        new Dictionary<int, TerrainState>();
    private readonly Dictionary<int, RendererState> _hiddenDiggerRenderers =
        new Dictionary<int, RendererState>();
    private readonly Dictionary<int, TargetRendererState> _targetRendererStates =
        new Dictionary<int, TargetRendererState>();
    private readonly List<DiggerState> _diggerSystems = new List<DiggerState>();
    private readonly List<Target> _targets = new List<Target>();
    private readonly HashSet<int> _targetIds = new HashSet<int>();
    private readonly List<TargetType> _targetTypes = new List<TargetType>();

    private readonly float[] _distances = { 15f, 30f, 50f, 75f, 100f };
    private readonly float[] _opacities = { 1f, 0.75f, 0.5f, 0.25f };

    private readonly string[] _chestTypes =
    {
        "Chest", "TreasureChest", "CarriedTreasureChest",
        "TreasureChestNPC", "DiggingWaypointChest"
    };

    private readonly string[] _discoverableTypes =
    {
        "CollectableLoreLog", "DiggingWaypointLoreLog", "DiggingWaypointBarrel",
        "DiggingWaypointSimpleItem", "DiggingWaypointPet", "DiggingWaypointDistanceItem",
        "UnstableMineral", "PickupItemStack", "ItemBarrel"
    };

    private bool _active;
    private bool _terrainHidden;
    private bool _overlay = true;
    private TargetMode _targetMode = TargetMode.Chests;
    private int _distanceIndex = 4;
    private int _opacityIndex = 3;

    private Type _diggerSystemType;
    private Type _localizationSettingsType;
    private PropertyInfo _selectedLocaleProperty;
    private float _nextLanguageCheck;
    private float _nextTerrainRecapture;
    private string _language = "en";

    private Material _chestMaterial;
    private Material _loreMaterial;
    private Material _itemMaterial;
    private GUIStyle _overlayStyle;

    private void Awake()
    {
        Logger.LogInfo("Stonewards X-Ray by sawet 1.1.0 loaded.");
        _diggerSystemType = FindType("Digger.Modules.Core.Sources.DiggerSystem");
        ResolveTargetTypes();
        BuildMaterials();
        UpdateLanguage(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.X)) ToggleMod();
        if (Input.GetKeyDown(KeyCode.F5) && _active) ToggleTerrain();

        if (Input.GetKeyDown(KeyCode.F6))
        {
            _targetMode = (TargetMode)(((int)_targetMode + 1) % 3);
            if (_active) RefreshTargets();
        }

        if (Input.GetKeyDown(KeyCode.F7))
        {
            _distanceIndex = (_distanceIndex + 1) % _distances.Length;
            if (_active) UpdateTargetVisibility();
        }

        if (Input.GetKeyDown(KeyCode.F8))
        {
            _opacityIndex = (_opacityIndex + 1) % _opacities.Length;
            ApplyOpacity();
        }

        if (Input.GetKeyDown(KeyCode.F9)) _overlay = !_overlay;

        if (_active) UpdateTargetVisibility();
        UpdateLanguage(false);
    }

    private void LateUpdate()
    {
        if (_active && _terrainHidden) EnforceHiddenTerrain();
    }

    private void OnDestroy()
    {
        RestoreTargets();
        RestoreTerrain();
        DestroyMaterial(ref _chestMaterial);
        DestroyMaterial(ref _loreMaterial);
        DestroyMaterial(ref _itemMaterial);
    }

    private void ToggleMod()
    {
        if (_active)
        {
            RestoreTargets();
            RestoreTerrain();
            _active = false;
            _terrainHidden = false;
            return;
        }

        _active = true;
        _terrainHidden = false;
        RestoreTerrain();
        RestoreTargets();
        ScanTargets();
        UpdateTargetVisibility();
    }

    private void ToggleTerrain()
    {
        if (_terrainHidden)
        {
            RestoreTerrain();
            _terrainHidden = false;
        }
        else
        {
            HideTerrain();
            _terrainHidden = true;
        }
    }

    private void RefreshTargets()
    {
        RestoreTargets();
        ScanTargets();
        UpdateTargetVisibility();
    }

    private void ResolveTargetTypes()
    {
        _targetTypes.Clear();
        AddTargetTypes(_chestTypes, TargetGroup.Chest);
        AddTargetTypes(_discoverableTypes, TargetGroup.Discoverable);
        Logger.LogInfo("Resolved " + _targetTypes.Count + " target component types.");
    }

    private void AddTargetTypes(string[] names, TargetGroup group)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Type type = FindType(names[i]);
            if (type == null) continue;

            TargetType entry = new TargetType();
            entry.Type = type;
            entry.Group = group;
            entry.Category = GetCategory(names[i]);
            _targetTypes.Add(entry);
        }
    }

    private bool Includes(TargetGroup group)
    {
        if (_targetMode == TargetMode.Chests) return group == TargetGroup.Chest;
        if (_targetMode == TargetMode.Discoverables) return group == TargetGroup.Discoverable;
        return true;
    }

    private void HideTerrain()
    {
        _terrains.Clear();
        _hiddenDiggerRenderers.Clear();
        _diggerSystems.Clear();

        UnityEngine.Object[] terrains = FindActiveObjects(typeof(Terrain));
        if (terrains != null)
        {
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i] as Terrain;
                if (terrain == null) continue;

                TerrainState state = new TerrainState();
                state.Terrain = terrain;
                state.Heightmap = terrain.drawHeightmap;
                state.Foliage = terrain.drawTreesAndFoliage;
                _terrains[terrain.GetInstanceID()] = state;

                terrain.drawHeightmap = false;
                terrain.drawTreesAndFoliage = false;
            }
        }

        CaptureDiggerRenderers();
    }

    private void CaptureDiggerRenderers()
    {
        if (_diggerSystemType == null)
            _diggerSystemType = FindType("Digger.Modules.Core.Sources.DiggerSystem");
        if (_diggerSystemType == null) return;

        UnityEngine.Object[] systems = FindActiveObjects(_diggerSystemType);
        if (systems == null) return;

        _diggerSystems.Clear();

        for (int i = 0; i < systems.Length; i++)
        {
            Component system = systems[i] as Component;
            if (system == null) continue;

            DiggerState ds = new DiggerState();
            ds.System = system;
            ds.ChildCount = system.transform.childCount;
            _diggerSystems.Add(ds);

            Renderer[] renderers = system.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null) continue;

                int id = renderer.GetInstanceID();
                if (!_hiddenDiggerRenderers.ContainsKey(id))
                {
                    RendererState state = new RendererState();
                    state.Renderer = renderer;
                    state.Enabled = renderer.enabled;
                    _hiddenDiggerRenderers[id] = state;
                }
                renderer.enabled = false;
            }
        }
    }

    private void EnforceHiddenTerrain()
    {
        bool recapture = false;

        foreach (TerrainState state in _terrains.Values)
        {
            if (state == null || state.Terrain == null) continue;
            state.Terrain.drawHeightmap = false;
            state.Terrain.drawTreesAndFoliage = false;
        }

        foreach (RendererState state in _hiddenDiggerRenderers.Values)
        {
            if (state == null || state.Renderer == null)
            {
                recapture = true;
                continue;
            }
            if (state.Renderer.enabled) state.Renderer.enabled = false;
        }

        for (int i = 0; i < _diggerSystems.Count; i++)
        {
            DiggerState state = _diggerSystems[i];
            if (state == null || state.System == null ||
                state.System.transform.childCount != state.ChildCount)
            {
                recapture = true;
                break;
            }
        }

        if (recapture && Time.unscaledTime >= _nextTerrainRecapture)
        {
            _nextTerrainRecapture = Time.unscaledTime + 0.25f;
            CaptureDiggerRenderers();
        }
    }

    private void RestoreTerrain()
    {
        foreach (TerrainState state in _terrains.Values)
        {
            if (state == null || state.Terrain == null) continue;
            state.Terrain.drawHeightmap = state.Heightmap;
            state.Terrain.drawTreesAndFoliage = state.Foliage;
        }

        foreach (RendererState state in _hiddenDiggerRenderers.Values)
        {
            if (state == null || state.Renderer == null) continue;
            state.Renderer.enabled = state.Enabled;
        }

        _terrains.Clear();
        _hiddenDiggerRenderers.Clear();
        _diggerSystems.Clear();
    }

    private void ScanTargets()
    {
        _targets.Clear();
        _targetIds.Clear();

        for (int t = 0; t < _targetTypes.Count; t++)
        {
            TargetType entry = _targetTypes[t];
            if (!Includes(entry.Group)) continue;

            UnityEngine.Object[] objects = FindActiveObjects(entry.Type);
            if (objects == null) continue;

            for (int i = 0; i < objects.Length; i++)
            {
                Component component = objects[i] as Component;
                if (component == null || component.gameObject == null ||
                    !component.gameObject.activeInHierarchy) continue;

                int id = component.gameObject.GetInstanceID();
                if (!_targetIds.Add(id)) continue;

                Target target = new Target();
                target.Transform = component.transform;
                target.Renderers = component.GetComponentsInChildren<Renderer>(true);
                target.Category = entry.Category;
                _targets.Add(target);
            }
        }
    }

    private string GetCategory(string typeName)
    {
        if (typeName.IndexOf("Chest", StringComparison.OrdinalIgnoreCase) >= 0) return "CHEST";
        if (typeName.IndexOf("Lore", StringComparison.OrdinalIgnoreCase) >= 0) return "LORE";
        return "ITEM";
    }

    private void UpdateTargetVisibility()
    {
        if (!_active) return;
        Camera camera = Camera.main;
        if (camera == null) return;

        Vector3 cameraPosition = camera.transform.position;
        float maxDistanceSqr = _distances[_distanceIndex] * _distances[_distanceIndex];

        for (int i = 0; i < _targets.Count; i++)
        {
            Target target = _targets[i];
            if (target == null || target.Transform == null) continue;

            bool visible =
                (target.Transform.position - cameraPosition).sqrMagnitude <= maxDistanceSqr;
            if (visible == target.Highlighted) continue;

            SetTargetHighlight(target, visible);
            target.Highlighted = visible;
        }
    }

    private void SetTargetHighlight(Target target, bool enabled)
    {
        if (target.Renderers == null) return;
        Material highlight = GetMaterial(target.Category);

        for (int i = 0; i < target.Renderers.Length; i++)
        {
            Renderer renderer = target.Renderers[i];
            if (renderer == null) continue;

            int id = renderer.GetInstanceID();
            TargetRendererState state;
            if (!_targetRendererStates.TryGetValue(id, out state))
            {
                state = new TargetRendererState();
                state.Renderer = renderer;
                state.Enabled = renderer.enabled;
                state.Materials = renderer.sharedMaterials;
                _targetRendererStates[id] = state;
            }

            if (!enabled)
            {
                renderer.sharedMaterials = state.Materials;
                renderer.enabled = state.Enabled;
                continue;
            }

            if (highlight == null) continue;
            int count = state.Materials != null && state.Materials.Length > 0
                ? state.Materials.Length : 1;
            Material[] materials = new Material[count];
            for (int m = 0; m < count; m++) materials[m] = highlight;
            renderer.sharedMaterials = materials;
            renderer.enabled = true;
        }
    }

    private void RestoreTargets()
    {
        foreach (TargetRendererState state in _targetRendererStates.Values)
        {
            if (state == null || state.Renderer == null) continue;
            state.Renderer.sharedMaterials = state.Materials;
            state.Renderer.enabled = state.Enabled;
        }
        _targetRendererStates.Clear();
        for (int i = 0; i < _targets.Count; i++)
            if (_targets[i] != null) _targets[i].Highlighted = false;
    }

    private void BuildMaterials()
    {
        _chestMaterial = CreateMaterial(new Color(1f, 0.75f, 0.05f, 1f));
        _loreMaterial = CreateMaterial(new Color(0.1f, 0.95f, 1.0f, 1f));
        _itemMaterial = CreateMaterial(new Color(1f, 0.25f, 0.9f, 1f));
        ApplyOpacity();
    }

    private Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored") ??
            Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (shader == null) return null;

        Material material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
        material.renderQueue = 5000;
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 3f);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", 8);
        if (material.HasProperty("_Cull")) material.SetInt("_Cull", 0);
        return material;
    }

    private void ApplyOpacity()
    {
        SetAlpha(_chestMaterial, _opacities[_opacityIndex]);
        SetAlpha(_loreMaterial, _opacities[_opacityIndex]);
        SetAlpha(_itemMaterial, _opacities[_opacityIndex]);
    }

    private void SetAlpha(Material material, float alpha)
    {
        if (material == null) return;
        if (material.HasProperty("_Color"))
        {
            Color c = material.GetColor("_Color"); c.a = alpha; material.SetColor("_Color", c);
        }
        if (material.HasProperty("_BaseColor"))
        {
            Color c = material.GetColor("_BaseColor"); c.a = alpha; material.SetColor("_BaseColor", c);
        }
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", 5);
        if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", 10);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        material.renderQueue = 5000;
    }

    private Material GetMaterial(string category)
    {
        if (category == "CHEST") return _chestMaterial;
        if (category == "LORE") return _loreMaterial;
        return _itemMaterial;
    }

    private void DestroyMaterial(ref Material material)
    {
        if (material == null) return;
        UnityEngine.Object.Destroy(material);
        material = null;
    }

    private void UpdateLanguage(bool force)
    {
        if (!force && Time.unscaledTime < _nextLanguageCheck) return;
        _nextLanguageCheck = Time.unscaledTime + 2f;

        try
        {
            if (_selectedLocaleProperty == null)
            {
                if (_localizationSettingsType == null)
                    _localizationSettingsType =
                        FindType("UnityEngine.Localization.Settings.LocalizationSettings");
                if (_localizationSettingsType != null)
                    _selectedLocaleProperty = _localizationSettingsType.GetProperty(
                        "SelectedLocale", BindingFlags.Public | BindingFlags.Static);
            }
            if (_selectedLocaleProperty == null) return;

            object locale = _selectedLocaleProperty.GetValue(null, null);
            if (locale == null) return;
            PropertyInfo identifierProperty = locale.GetType().GetProperty("Identifier");
            if (identifierProperty == null) return;
            object identifier = identifierProperty.GetValue(locale, null);
            if (identifier == null) return;
            PropertyInfo codeProperty = identifier.GetType().GetProperty("Code");
            if (codeProperty == null) return;
            string code = codeProperty.GetValue(identifier, null) as string;
            if (!string.IsNullOrEmpty(code)) _language = NormalizeLanguage(code);
        }
        catch (Exception ex)
        {
            Logger.LogDebug("Locale detection failed: " + ex.Message);
        }
    }

    private string NormalizeLanguage(string code)
    {
        string value = code.Trim().ToLowerInvariant().Replace('_', '-');
        if (value.StartsWith("zh-hans") || value.StartsWith("zh-cn") || value.StartsWith("zh-sg")) return "zh-hans";
        if (value.StartsWith("zh-hant") || value.StartsWith("zh-tw") || value.StartsWith("zh-hk")) return "zh-hant";
        if (value.StartsWith("fr")) return "fr";
        if (value.StartsWith("de")) return "de";
        if (value.StartsWith("es")) return "es";
        if (value.StartsWith("ja")) return "ja";
        if (value.StartsWith("pt")) return "pt";
        if (value.StartsWith("ru")) return "ru";
        return "en";
    }

    private string T(int key)
    {
        string[] text;
        if (!Ui.TryGetValue(_language, out text)) text = Ui["en"];
        return text[key];
    }

    private string ModeText()
    {
        if (_targetMode == TargetMode.Chests) return T(UiChests);
        if (_targetMode == TargetMode.Discoverables) return T(UiDiscoverables);
        return T(UiBoth);
    }

    private UnityEngine.Object[] FindActiveObjects(Type type)
    {
        if (type == null) return null;
        try
        {
            MethodInfo[] methods = typeof(UnityEngine.Object).GetMethods(
                BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != "FindObjectsOfType") continue;
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Type))
                    return methods[i].Invoke(null, new object[] { type }) as UnityEngine.Object[];
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("FindObjectsOfType failed for " + type.FullName + ": " + ex.Message);
        }
        return null;
    }

    private static Type FindType(string name)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            try
            {
                Type direct = assemblies[i].GetType(name, false);
                if (direct != null) return direct;
            }
            catch { }

            try
            {
                Type[] types = assemblies[i].GetTypes();
                for (int t = 0; t < types.Length; t++)
                    if (types[t] != null && (types[t].Name == name || types[t].FullName == name))
                        return types[t];
            }
            catch (ReflectionTypeLoadException ex)
            {
                if (ex.Types == null) continue;
                for (int t = 0; t < ex.Types.Length; t++)
                    if (ex.Types[t] != null && (ex.Types[t].Name == name || ex.Types[t].FullName == name))
                        return ex.Types[t];
            }
            catch { }
        }
        return null;
    }

    private void EnsureStyle()
    {
        if (_overlayStyle != null) return;
        _overlayStyle = new GUIStyle(GUI.skin.label);
        _overlayStyle.fontSize = 13;
        _overlayStyle.normal.textColor = Color.white;
        _overlayStyle.wordWrap = true;
    }

    private void OnGUI()
    {
        if (!_overlay) return;
        EnsureStyle();

        GUI.Box(new Rect(12f, 12f, 600f, 100f), "");
        GUI.Label(
            new Rect(22f, 18f, 580f, 90f),
            "Stonewards X-Ray by sawet\n" +
            "X " + T(UiXRay) + " [" + T(_active ? UiOn : UiOff) + "]" +
            "   F5 " + T(UiTerrain) + " [" + T(_terrainHidden ? UiOff : UiOn) + "]\n" +
            "F6 " + T(UiTarget) + " [" + ModeText() + "]" +
            "   F7 " + T(UiDistance) + " [" + _distances[_distanceIndex].ToString("0") + "m]\n" +
            "F8 " + T(UiOpacity) + " [" + (_opacities[_opacityIndex] * 100f).ToString("0") + "%]" +
            "   F9 " + T(UiOverlay) + " [" + T(UiOn) + "]",
            _overlayStyle);
    }
}
