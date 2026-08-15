using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using UnityEngine;

[BepInPlugin("sawet.stonewards.xray", "Stonewards X-Ray by sawet", "1.0.0")]
public class StonewardsXRayPlugin : BaseUnityPlugin
{
    private enum TargetMode
    {
        ChestsOnly = 0,
        Discoverables = 1,
        EverythingInteresting = 2
    }

    private sealed class TerrainState
    {
        public Terrain Terrain;
        public bool DrawHeightmap;
        public bool DrawTreesAndFoliage;
    }

    private sealed class RendererEnabledState
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

    private sealed class Target
    {
        public Component Component;
        public Transform Transform;
        public string Category;
        public string TypeName;
        public bool IsWaypoint;
        public Renderer[] Renderers;
        public bool Highlighted;
    }

    private sealed class ResolvedTargetType
    {
        public Type Type;
        public string TypeName;
        public string Category;
        public bool IsWaypoint;
        public TargetMode MinimumMode;
    }

    private readonly Dictionary<int, TerrainState> _terrainStates =
        new Dictionary<int, TerrainState>();

    private readonly Dictionary<int, RendererEnabledState> _hiddenDiggerRenderers =
        new Dictionary<int, RendererEnabledState>();

    private readonly Dictionary<int, TargetRendererState> _targetRendererStates =
        new Dictionary<int, TargetRendererState>();

    private readonly List<Target> _targets = new List<Target>();
    private readonly HashSet<int> _targetObjectIds = new HashSet<int>();
    private readonly List<ResolvedTargetType> _resolvedTargetTypes =
        new List<ResolvedTargetType>();

    private bool _modActive = false;
    private bool _trueXRay = false;
    private bool _labels = true;
    private bool _overlay = true;
    private TargetMode _targetMode = TargetMode.Discoverables;

    private readonly float[] _revealDistances = new float[]
    {
        15f, 30f, 50f, 75f, 100f, float.PositiveInfinity
    };

    private readonly float[] _revealOpacities = new float[]
    {
        1.00f, 0.75f, 0.50f, 0.25f
    };

    private int _distanceIndex = 1; // 30 m default
    private int _opacityIndex = 1; // 75% default

    private Type _diggerSystemType;

    private Material _chestMaterial;
    private Material _loreMaterial;
    private Material _itemMaterial;
    private Material _dangerMaterial;

    private GUIStyle _overlayStyle;
    private GUIStyle _labelStyle;

    private string _status = "Ready.";
    private int _terrainCount;
    private int _hiddenRendererCount;
    private int _chestCount;
    private int _waypointCount;
    private int _discoverableCount;

    private readonly string[] _chestTypes = new string[]
    {
        "Chest",
        "TreasureChest",
        "CarriedTreasureChest",
        "TreasureChestNPC",
        "DiggingWaypointChest"
    };

    private readonly string[] _discoverableTypes = new string[]
    {
        "CollectableLoreLog",
        "DiggingWaypointLoreLog",
        "DiggingWaypointBarrel",
        "DiggingWaypointSimpleItem",
        "DiggingWaypointPet",
        "DiggingWaypointDestructible",
        "DiggingWaypointDistanceItem",
        "UnstableMineral",
        "PickupItemStack",
        "ItemBarrel"
    };

    private readonly string[] _extraTypes = new string[]
    {
        "DiggingWaypointLamp",
        "DiggingWaypointLog",
        "DiggingWaypointEnemy",
        "DiggingWaypointHazard",
        "DestructibleObject",
        "DestructibleProp"
    };

    private void Awake()
    {
        Logger.LogInfo("Stonewards X-Ray by sawet 1.0.0 loaded.");

        _diggerSystemType = FindType("Digger.Modules.Core.Sources.DiggerSystem");

        ResolveTargetTypes();
        BuildMaterials();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            ToggleMod();
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            ToggleXRay();
        }

        if (Input.GetKeyDown(KeyCode.F6))
        {
            int next = ((int)_targetMode + 1) % 3;
            _targetMode = (TargetMode)next;

            if (_modActive)
                RefreshTargetsOnly();
        }

        if (Input.GetKeyDown(KeyCode.F7))
        {
            _labels = !_labels;
        }

        if (Input.GetKeyDown(KeyCode.F8))
        {
            _opacityIndex = (_opacityIndex + 1) % _revealOpacities.Length;
            ApplyOpacityToMaterials();

            if (_modActive)
                UpdateTargetVisibility();
        }

        if (Input.GetKeyDown(KeyCode.F9))
        {
            _distanceIndex = (_distanceIndex + 1) % _revealDistances.Length;

            if (_modActive)
                UpdateTargetVisibility();
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            _overlay = !_overlay;
        }

        // Only cached target transforms are checked here.
        // No timed world scan or reflection runs during normal gameplay.
        if (_modActive)
            UpdateTargetVisibility();
    }

    private void OnDestroy()
    {
        RestoreTargetRenderers();
        RestoreWorldRendering();

        DestroyMaterial(ref _chestMaterial);
        DestroyMaterial(ref _loreMaterial);
        DestroyMaterial(ref _itemMaterial);
        DestroyMaterial(ref _dangerMaterial);
    }

    private void ToggleMod()
    {
        if (_modActive)
            DisableMod();
        else
            EnableMod();
    }

    private void EnableMod()
    {
        try
        {
            _modActive = true;
            _trueXRay = false;

            // Default active mode keeps terrain visible.
            RestoreWorldRendering();
            RestoreTargetRenderers();

            // A fresh scan on activation also serves as the manual rescan path
            // for newly generated mine areas.
            ScanTargets();
            ApplyTargetHighlights();

            _status = "Mod active.";
            LogRefreshSummary("active");
        }
        catch (Exception ex)
        {
            _status = "Enable error: " + ex.GetType().Name;
            Logger.LogError(ex);
        }
    }

    private void DisableMod()
    {
        try
        {
            RestoreTargetRenderers();
            RestoreWorldRendering();

            _modActive = false;
            _trueXRay = false;
            _status = "Mod inactive.";

            Logger.LogInfo("Stonewards X-Ray deactivated.");
        }
        catch (Exception ex)
        {
            _status = "Disable error: " + ex.GetType().Name;
            Logger.LogError(ex);
        }
    }

    private void ToggleXRay()
    {
        try
        {
            if (!_modActive)
                EnableMod();

            if (_trueXRay)
            {
                RestoreWorldRendering();
                _trueXRay = false;
                _status = "X-Ray off. Terrain visible.";
            }
            else
            {
                HideWorldRendering();
                _trueXRay = true;
                _status = "X-Ray on. Terrain hidden.";
            }
        }
        catch (Exception ex)
        {
            _status = "X-Ray toggle error: " + ex.GetType().Name;
            Logger.LogError(ex);
        }
    }

    private void RefreshTargetsOnly()
    {
        try
        {
            RestoreTargetRenderers();
            ScanTargets();

            if (_modActive)
                ApplyTargetHighlights();

            _status = "Targets rescanned. Mode: " + ModeText();
            LogRefreshSummary("targets");
        }
        catch (Exception ex)
        {
            _status = "Target scan error: " + ex.GetType().Name;
            Logger.LogError(ex);
        }
    }

    private void ResolveTargetTypes()
    {
        _resolvedTargetTypes.Clear();

        AddResolvedTypes(_chestTypes, TargetMode.ChestsOnly);
        AddResolvedTypes(_discoverableTypes, TargetMode.Discoverables);
        AddResolvedTypes(_extraTypes, TargetMode.EverythingInteresting);

        Logger.LogInfo(
            "Resolved " + _resolvedTargetTypes.Count +
            " Stonewards target component types once at startup."
        );
    }

    private void AddResolvedTypes(string[] names, TargetMode minimumMode)
    {
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            Type type = FindType(name);

            if (type == null)
                continue;

            ResolvedTargetType entry = new ResolvedTargetType();
            entry.Type = type;
            entry.TypeName = name;
            entry.Category = GetCategory(name);
            entry.IsWaypoint =
                name.IndexOf("Waypoint", StringComparison.OrdinalIgnoreCase) >= 0;
            entry.MinimumMode = minimumMode;

            _resolvedTargetTypes.Add(entry);
        }
    }

    private void HideWorldRendering()
    {
        _terrainCount = 0;
        _hiddenRendererCount = 0;

        UnityEngine.Object[] terrains = FindActiveUnityObjects(typeof(Terrain));

        if (terrains != null)
        {
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i] as Terrain;
                if (terrain == null)
                    continue;

                _terrainCount++;

                int id = terrain.GetInstanceID();

                if (!_terrainStates.ContainsKey(id))
                {
                    TerrainState state = new TerrainState();
                    state.Terrain = terrain;
                    state.DrawHeightmap = terrain.drawHeightmap;
                    state.DrawTreesAndFoliage = terrain.drawTreesAndFoliage;
                    _terrainStates[id] = state;
                }

                terrain.drawHeightmap = false;
                terrain.drawTreesAndFoliage = false;
            }
        }

        if (_diggerSystemType == null)
            _diggerSystemType = FindType("Digger.Modules.Core.Sources.DiggerSystem");

        if (_diggerSystemType == null)
            return;

        UnityEngine.Object[] systems = FindActiveUnityObjects(_diggerSystemType);

        if (systems == null)
            return;

        for (int i = 0; i < systems.Length; i++)
        {
            Component system = systems[i] as Component;
            if (system == null)
                continue;

            Renderer[] renderers = system.GetComponentsInChildren<Renderer>(true);
            if (renderers == null)
                continue;

            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null)
                    continue;

                int id = renderer.GetInstanceID();

                if (!_hiddenDiggerRenderers.ContainsKey(id))
                {
                    RendererEnabledState state = new RendererEnabledState();
                    state.Renderer = renderer;
                    state.Enabled = renderer.enabled;
                    _hiddenDiggerRenderers[id] = state;
                }

                renderer.enabled = false;
                _hiddenRendererCount++;
            }
        }
    }

    private void RestoreWorldRendering()
    {
        foreach (KeyValuePair<int, TerrainState> pair in _terrainStates)
        {
            TerrainState state = pair.Value;

            if (state == null || state.Terrain == null)
                continue;

            state.Terrain.drawHeightmap = state.DrawHeightmap;
            state.Terrain.drawTreesAndFoliage = state.DrawTreesAndFoliage;
        }

        foreach (KeyValuePair<int, RendererEnabledState> pair in _hiddenDiggerRenderers)
        {
            RendererEnabledState state = pair.Value;

            if (state == null || state.Renderer == null)
                continue;

            state.Renderer.enabled = state.Enabled;
        }

        _terrainStates.Clear();
        _hiddenDiggerRenderers.Clear();

        _terrainCount = 0;
        _hiddenRendererCount = 0;
    }

    private void ScanTargets()
    {
        _targets.Clear();
        _targetObjectIds.Clear();

        _chestCount = 0;
        _waypointCount = 0;
        _discoverableCount = 0;

        for (int t = 0; t < _resolvedTargetTypes.Count; t++)
        {
            ResolvedTargetType entry = _resolvedTargetTypes[t];

            if ((int)_targetMode < (int)entry.MinimumMode)
                continue;

            // v0.2.1 used Resources.FindObjectsOfTypeAll here.
            // We only want live scene objects, so active-only scanning avoids
            // walking project assets/prefabs and creates much less GC pressure.
            UnityEngine.Object[] objects = FindActiveUnityObjects(entry.Type);

            if (objects == null)
                continue;

            for (int i = 0; i < objects.Length; i++)
            {
                Component component = objects[i] as Component;

                if (component == null ||
                    component.gameObject == null ||
                    !component.gameObject.activeInHierarchy)
                    continue;

                int objectId = component.gameObject.GetInstanceID();

                if (_targetObjectIds.Contains(objectId))
                    continue;

                _targetObjectIds.Add(objectId);

                Target target = new Target();
                target.Component = component;
                target.Transform = component.transform;
                target.TypeName = entry.TypeName;
                target.Category = entry.Category;
                target.IsWaypoint = entry.IsWaypoint;
                target.Renderers = component.GetComponentsInChildren<Renderer>(true);
                target.Highlighted = false;

                _targets.Add(target);

                if (target.Category == "CHEST")
                    _chestCount++;
                else
                    _discoverableCount++;

                if (target.IsWaypoint)
                    _waypointCount++;
            }
        }
    }

    private string GetCategory(string typeName)
    {
        if (typeName.IndexOf("Chest", StringComparison.OrdinalIgnoreCase) >= 0)
            return "CHEST";

        if (typeName.IndexOf("Lore", StringComparison.OrdinalIgnoreCase) >= 0)
            return "LORE";

        if (typeName.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0 ||
            typeName.IndexOf("Hazard", StringComparison.OrdinalIgnoreCase) >= 0)
            return "DANGER";

        if (typeName.IndexOf("Pet", StringComparison.OrdinalIgnoreCase) >= 0)
            return "PET";

        if (typeName.IndexOf("Mineral", StringComparison.OrdinalIgnoreCase) >= 0)
            return "MINERAL";

        if (typeName.IndexOf("Barrel", StringComparison.OrdinalIgnoreCase) >= 0)
            return "BARREL";

        if (typeName.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0)
            return "ITEM";

        if (typeName.IndexOf("Destruct", StringComparison.OrdinalIgnoreCase) >= 0)
            return "DESTRUCTIBLE";

        return "DISCOVERY";
    }

    private void ApplyTargetHighlights()
    {
        // Materials are applied only to cached targets that are currently inside
        // the selected reveal distance.
        UpdateTargetVisibility();
    }

    private void UpdateTargetVisibility()
    {
        if (!_modActive)
            return;

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Vector3 cameraPosition = camera.transform.position;
        float maxDistance = _revealDistances[_distanceIndex];
        bool unlimited = float.IsPositiveInfinity(maxDistance);
        float maxDistanceSqr = unlimited ? float.PositiveInfinity : maxDistance * maxDistance;

        for (int i = 0; i < _targets.Count; i++)
        {
            Target target = _targets[i];

            if (target == null || target.Transform == null)
                continue;

            bool inRange =
                unlimited ||
                (target.Transform.position - cameraPosition).sqrMagnitude <= maxDistanceSqr;

            if (inRange == target.Highlighted)
                continue;

            SetTargetHighlight(target, inRange);
            target.Highlighted = inRange;
        }
    }

    private void SetTargetHighlight(Target target, bool enabled)
    {
        if (target == null || target.Renderers == null)
            return;

        Material highlight = GetMaterialForCategory(target.Category);

        for (int r = 0; r < target.Renderers.Length; r++)
        {
            Renderer renderer = target.Renderers[r];

            if (renderer == null)
                continue;

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

            if (enabled)
            {
                if (highlight == null)
                    continue;

                Material[] current = state.Materials;
                int count =
                    current != null && current.Length > 0
                        ? current.Length
                        : 1;

                Material[] replacement = new Material[count];

                for (int m = 0; m < replacement.Length; m++)
                    replacement[m] = highlight;

                renderer.sharedMaterials = replacement;
                renderer.enabled = true;
            }
            else
            {
                renderer.sharedMaterials = state.Materials;
                renderer.enabled = state.Enabled;
            }
        }
    }

    private void RestoreTargetRenderers()
    {
        foreach (KeyValuePair<int, TargetRendererState> pair in _targetRendererStates)
        {
            TargetRendererState state = pair.Value;

            if (state == null || state.Renderer == null)
                continue;

            state.Renderer.sharedMaterials = state.Materials;
            state.Renderer.enabled = state.Enabled;
        }

        _targetRendererStates.Clear();

        for (int i = 0; i < _targets.Count; i++)
        {
            if (_targets[i] != null)
                _targets[i].Highlighted = false;
        }
    }

    private void BuildMaterials()
    {
        _chestMaterial =
            CreateXRayMaterial(new Color(1.0f, 0.75f, 0.05f, 1f));

        _loreMaterial =
            CreateXRayMaterial(new Color(0.1f, 0.95f, 1.0f, 1f));

        _itemMaterial =
            CreateXRayMaterial(new Color(1.0f, 0.25f, 0.9f, 1f));

        _dangerMaterial =
            CreateXRayMaterial(new Color(1.0f, 0.1f, 0.1f, 1f));

        ApplyOpacityToMaterials();
    }

    private Material CreateXRayMaterial(Color color)
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
        {
            Logger.LogWarning(
                "Could not find an unlit shader for X-Ray highlighting."
            );
            return null;
        }

        Material material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
        material.renderQueue = 5000;

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", color * 3f);

        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);

        if (material.HasProperty("_ZTest"))
            material.SetInt("_ZTest", 8);

        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", 0);

        return material;
    }

    private void ApplyOpacityToMaterials()
    {
        float alpha = _revealOpacities[_opacityIndex];

        SetMaterialAlpha(_chestMaterial, alpha);
        SetMaterialAlpha(_loreMaterial, alpha);
        SetMaterialAlpha(_itemMaterial, alpha);
        SetMaterialAlpha(_dangerMaterial, alpha);
    }

    private void SetMaterialAlpha(Material material, float alpha)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Color"))
        {
            Color color = material.GetColor("_Color");
            color.a = alpha;
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            Color color = material.GetColor("_BaseColor");
            color.a = alpha;
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", 5); // SrcAlpha

        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", 10); // OneMinusSrcAlpha

        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);

        material.renderQueue = 5000;
    }

    private string DistanceText()
    {
        float distance = _revealDistances[_distanceIndex];

        if (float.IsPositiveInfinity(distance))
            return "∞";

        return distance.ToString("0") + "m";
    }

    private string OpacityText()
    {
        return (_revealOpacities[_opacityIndex] * 100f).ToString("0") + "%";
    }

    private Material GetMaterialForCategory(string category)
    {
        if (category == "CHEST")
            return _chestMaterial;

        if (category == "LORE")
            return _loreMaterial;

        if (category == "DANGER")
            return _dangerMaterial;

        return _itemMaterial;
    }

    private void DestroyMaterial(ref Material material)
    {
        if (material != null)
        {
            UnityEngine.Object.Destroy(material);
            material = null;
        }
    }

    private UnityEngine.Object[] FindActiveUnityObjects(Type wantedType)
    {
        if (wantedType == null)
            return null;

        try
        {
            MethodInfo[] methods =
                typeof(UnityEngine.Object).GetMethods(
                    BindingFlags.Public | BindingFlags.Static
                );

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];

                if (method.Name != "FindObjectsOfType")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length == 1 &&
                    parameters[0].ParameterType == typeof(Type))
                {
                    object result =
                        method.Invoke(null, new object[] { wantedType });

                    return result as UnityEngine.Object[];
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                "FindObjectsOfType failed for " +
                wantedType.FullName +
                ": " +
                ex.Message
            );
        }

        return null;
    }

    private static Type FindType(string fullNameOrName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            Assembly assembly = assemblies[i];

            try
            {
                Type direct =
                    assembly.GetType(fullNameOrName, false);

                if (direct != null)
                    return direct;
            }
            catch
            {
            }

            try
            {
                Type[] types = assembly.GetTypes();

                for (int t = 0; t < types.Length; t++)
                {
                    Type type = types[t];

                    if (type == null)
                        continue;

                    if (type.Name == fullNameOrName ||
                        type.FullName == fullNameOrName)
                        return type;
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                Type[] types = ex.Types;

                if (types == null)
                    continue;

                for (int t = 0; t < types.Length; t++)
                {
                    Type type = types[t];

                    if (type == null)
                        continue;

                    if (type.Name == fullNameOrName ||
                        type.FullName == fullNameOrName)
                        return type;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private void LogRefreshSummary(string reason)
    {
        Logger.LogInfo(
            "X-Ray refresh (" + reason + "): " +
            "targets=" + _targets.Count +
            ", chests=" + _chestCount
        );
    }

    private void EnsureStyles()
    {
        if (_overlayStyle == null)
        {
            _overlayStyle = new GUIStyle(GUI.skin.label);
            _overlayStyle.fontSize = 13;
            _overlayStyle.normal.textColor = Color.white;
            _overlayStyle.wordWrap = true;
        }

        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 12;
            _labelStyle.normal.textColor = Color.white;
        }
    }

    private Color ColorForCategory(string category)
    {
        if (category == "CHEST")
            return new Color(1.0f, 0.75f, 0.05f, 1f);

        if (category == "LORE")
            return new Color(0.1f, 0.95f, 1.0f, 1f);

        if (category == "DANGER")
            return new Color(1.0f, 0.1f, 0.1f, 1f);

        return new Color(1.0f, 0.25f, 0.9f, 1f);
    }

    private string ModeText()
    {
        if (_targetMode == TargetMode.ChestsOnly)
            return "Chests";

        if (_targetMode == TargetMode.Discoverables)
            return "Chests + Discoverables";

        return "All";
    }

    private void OnGUI()
    {
        if (!_overlay && !_labels)
            return;

        EnsureStyles();

        if (_overlay)
        {
            GUI.Box(new Rect(12f, 12f, 575f, 100f), "");

            GUI.Label(
                new Rect(22f, 18f, 555f, 90f),
                "Stonewards X-Ray by sawet\n" +
                "F5 Mod [" + (_modActive ? "ACTIVE" : "INACTIVE") + "]" +
                "   X X-Ray [" + (_trueXRay ? "ON" : "OFF") + "]\n" +
                "F6 Targets [" + ModeText() + "]" +
                "   F7 Name Tags [" + (_labels ? "ON" : "OFF") + "]\n" +
                "F9 Distance [" + DistanceText() + "]" +
                "   F8 Opacity [" + OpacityText() + "]" +
                "   F10 Overlay",
                _overlayStyle
            );
        }

        if (!_labels || !_modActive)
            return;

        Camera camera = Camera.main;

        if (camera == null)
            return;

        Vector3 cameraPosition = camera.transform.position;

        for (int i = 0; i < _targets.Count; i++)
        {
            Target target = _targets[i];

            if (target == null || target.Transform == null)
                continue;

            Vector3 position = target.Transform.position;
            Vector3 screen = camera.WorldToScreenPoint(position);

            if (screen.z <= 0f)
                continue;

            float x = screen.x;
            float y = Screen.height - screen.y;

            if (x < -80f ||
                y < -30f ||
                x > Screen.width + 80f ||
                y > Screen.height + 30f)
                continue;

            float distance = Vector3.Distance(cameraPosition, position);
            float maxDistance = _revealDistances[_distanceIndex];

            if (!float.IsPositiveInfinity(maxDistance) && distance > maxDistance)
                continue;

            Color old = GUI.color;
            Color markerColor = ColorForCategory(target.Category);
            markerColor.a = _revealOpacities[_opacityIndex];
            GUI.color = markerColor;

            GUI.DrawTexture(
                new Rect(x - 4f, y - 4f, 8f, 8f),
                Texture2D.whiteTexture
            );

            string label =
                target.Category + "  " + distance.ToString("0") + "m";

            if (target.IsWaypoint)
                label += "  [WAYPOINT]";

            GUI.Label(
                new Rect(x + 7f, y - 12f, 320f, 24f),
                label,
                _labelStyle
            );

            GUI.color = old;
        }
    }
}
