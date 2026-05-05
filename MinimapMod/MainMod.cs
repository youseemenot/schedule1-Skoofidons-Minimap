using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(MinimapMod.MainMod), "Skoofidon's Minimap", "2.1.1", "Youseemenot (Orig. Hiccup)", null)]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonOptionalDependencies("ModManager&PhoneApp")]

namespace MinimapMod
{
    public class MainMod : MelonMod
    {
        public static MelonPreferences_Category prefCategory;
        public static MelonPreferences_Entry<bool> prefEnabled;
        public static MelonPreferences_Entry<bool> prefTime;
        public static MelonPreferences_Entry<bool> prefOrders;
        public static MelonPreferences_Entry<bool> prefDealers;
        public static MelonPreferences_Entry<bool> prefCustomers;
        public static MelonPreferences_Entry<bool> prefDeadDrops;
        public static MelonPreferences_Entry<bool> prefProperties;
        public static MelonPreferences_Entry<bool> prefVehicles;
        public static MelonPreferences_Entry<bool> prefCoop;
        public static MelonPreferences_Entry<bool> prefNavMode;
        public static MelonPreferences_Entry<float> prefWindowSize;
        public static MelonPreferences_Entry<float> prefZoom;
        public static MelonPreferences_Entry<float> prefOpacity;

        public static MelonPreferences_Entry<float> prefArrowSize;
        public static MelonPreferences_Entry<float> prefArrowR;
        public static MelonPreferences_Entry<float> prefArrowG;
        public static MelonPreferences_Entry<float> prefArrowB;

        private bool guiVisible = false;
        private bool minimapEnabled = true;
        private bool timeBarEnabled = true;
        private bool showOrdersEnabled = true;
        private bool showDealersEnabled = true;
        private bool showCustomersEnabled = true;
        private bool showDeadDropsEnabled = true;
        private bool showPropertiesEnabled = true;
        private bool showVehiclesEnabled = true;
        private bool coopRadarEnabled = true;
        private bool useNavMode = false;
        private float windowSizeMultiplier = 1f;
        private float mapZoom = 1f;
        private float mapOpacity = 1f;

        private float arrowSize = 1.4f;
        private float arrowR = 0f;
        private float arrowG = 0.8f;
        private float arrowB = 1f;

        private static float mapScale = 1.2487098f;
        private static int gridSize = 20;
        private static float smoothingFactor = 10f;

        private GameObject minimapObject;
        private GameObject minimapDisplayObject;
        private RectTransform minimapTimeContainer;
        private RectTransform minimapFrameRect;
        private static RectTransform gridContainer;
        private Text minimapTimeText;
        private Text cachedGameTimeText;
        private static Texture2D solidColorTex;

        private static GameObject mapAppObject;
        private static GameObject viewportObject;
        private static GameObject playerObject;
        private static GameObject mapContentObject;

        private GameObject cachedPlayerMarker;
        private GameObject cachedMapContent;

        private RectTransform cachedContentRt;
        private RectTransform cachedRotatorRt;
        private Transform cachedMinimapMask;
        private RectTransform cachedLocalPlayerUI; // Новое поле для фикса кооп-радара
        private readonly List<RectTransform> activeCoopPlayerRts = new List<RectTransform>();

        private List<GameObject> coopMarkers = new List<GameObject>();
        private Dictionary<Transform, GameObject> syncedPOIs = new Dictionary<Transform, GameObject>();

        private readonly List<Transform> currentKeysBuffer = new List<Transform>();
        private readonly List<Transform> validKeysBuffer = new List<Transform>();
        private static readonly char[] spaceSeparator = new char[] { ' ' };

        private static bool isInitializing = false;
        private static bool isEnabled = true;
        private bool isModManagerInstalled = false;
        private bool hasLoggedUpdateError = false;

        private static Color gridColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        public override void OnInitializeMelon()
        {
            prefCategory = MelonPreferences.CreateCategory("Skoofidon's Minimap", "Skoofidon's Minimap");

            prefEnabled = prefCategory.CreateEntry("EnableMinimap", true, "Enable Minimap", "Enable or disable the minimap UI.");
            prefTime = prefCategory.CreateEntry("EnableTime", true, "Time Display", "Show the current in-game time below the minimap.");
            prefOrders = prefCategory.CreateEntry("ShowOrders", true, "Show Orders", "Show green order markers.");
            prefDealers = prefCategory.CreateEntry("ShowDealers", true, "Show Dealers", "Show dealer markers.");
            prefCustomers = prefCategory.CreateEntry("ShowCustomers", true, "Show Customers", "Show potential customer markers.");
            prefDeadDrops = prefCategory.CreateEntry("ShowDeadDrops", true, "Show Dead Drops", "Show dead drop locations.");
            prefProperties = prefCategory.CreateEntry("ShowProperties", true, "Show Properties", "Show property markers.");
            prefVehicles = prefCategory.CreateEntry("ShowVehicles", true, "Show Vehicles", "Show owned vehicle markers.");
            prefCoop = prefCategory.CreateEntry("ShowCoop", true, "Show Co-op Players", "Show other players in Co-op.");
            prefNavMode = prefCategory.CreateEntry("NavMode", false, "Navigation Mode", "Map rotates to match your looking direction.");
            prefWindowSize = prefCategory.CreateEntry("WindowSize", 1.0f, "Window Size Multiplier", "Minimap window UI size (1.0 to 3.0).");
            prefZoom = prefCategory.CreateEntry("MapZoom", 1.0f, "Map Content Zoom", "Zoom scale of the map inside the window (0.5 to 3.0).");
            prefOpacity = prefCategory.CreateEntry("MapOpacity", 1.0f, "Minimap Opacity", "Minimap background opacity (0.2 to 1.0).");

            prefArrowSize = prefCategory.CreateEntry("ArrowSize", 1.4f, "Arrow Size", "Size of the player arrow marker.");
            prefArrowR = prefCategory.CreateEntry("ArrowR", 0.0f, "Arrow Color R", "Red color channel of arrow.");
            prefArrowG = prefCategory.CreateEntry("ArrowG", 0.8f, "Arrow Color G", "Green color channel of arrow.");
            prefArrowB = prefCategory.CreateEntry("ArrowB", 1.0f, "Arrow Color B", "Blue color channel of arrow.");

            foreach (var mod in MelonBase.RegisteredMelons)
            {
                if (mod.Info.Name == "Mod Manager & Phone App")
                {
                    isModManagerInstalled = true;
                    break;
                }
            }

            if (isModManagerInstalled)
            {
                SubscribeToModManagerEvents();
            }

            HandleSettingsUpdate();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SubscribeToModManagerEvents()
        {
            try
            {
                ModManagerPhoneApp.ModSettingsEvents.OnPhonePreferencesSaved += HandleSettingsUpdate;
                ModManagerPhoneApp.ModSettingsEvents.OnMenuPreferencesSaved += HandleSettingsUpdate;
            }
            catch (Exception)
            {
                // Игнорируем молча, если менеджера нет
            }
        }

        private void HandleSettingsUpdate()
        {
            minimapEnabled = prefEnabled.Value;
            timeBarEnabled = prefTime.Value;
            showOrdersEnabled = prefOrders.Value;
            showDealersEnabled = prefDealers.Value;
            showCustomersEnabled = prefCustomers.Value;
            showDeadDropsEnabled = prefDeadDrops.Value;
            showPropertiesEnabled = prefProperties.Value;
            showVehiclesEnabled = prefVehicles.Value;
            coopRadarEnabled = prefCoop.Value;
            useNavMode = prefNavMode.Value;

            windowSizeMultiplier = Mathf.Clamp(prefWindowSize.Value, 1f, 3f);
            mapZoom = Mathf.Clamp(prefZoom.Value, 0.5f, 3f);
            mapOpacity = Mathf.Clamp(prefOpacity.Value, 0.2f, 1f);

            arrowSize = Mathf.Clamp(prefArrowSize.Value, 0.5f, 3f);
            arrowR = Mathf.Clamp(prefArrowR.Value, 0f, 1f);
            arrowG = Mathf.Clamp(prefArrowG.Value, 0f, 1f);
            arrowB = Mathf.Clamp(prefArrowB.Value, 0f, 1f);

            UpdateMinimapSize();
            UpdateMinimapOpacity();

            if (mapContentObject != null)
            {
                mapContentObject.transform.localScale = new Vector3(mapZoom, mapZoom, 1f);
            }

            if (cachedPlayerMarker != null)
            {
                cachedPlayerMarker.GetComponent<RectTransform>().localScale = new Vector3(arrowSize, arrowSize, 1f);
                cachedPlayerMarker.GetComponent<Image>().color = new Color(arrowR, arrowG, arrowB, 1f);
            }

            if (minimapDisplayObject != null) minimapDisplayObject.SetActive(minimapEnabled);
            if (minimapTimeContainer != null) minimapTimeContainer.gameObject.SetActive(minimapEnabled && timeBarEnabled);
            if (!coopRadarEnabled) ClearCoopMarkers();
        }

        private void SaveSettingsFromF3()
        {
            prefEnabled.Value = minimapEnabled;
            prefTime.Value = timeBarEnabled;
            prefOrders.Value = showOrdersEnabled;
            prefDealers.Value = showDealersEnabled;
            prefCustomers.Value = showCustomersEnabled;
            prefDeadDrops.Value = showDeadDropsEnabled;
            prefProperties.Value = showPropertiesEnabled;
            prefVehicles.Value = showVehiclesEnabled;
            prefCoop.Value = coopRadarEnabled;
            prefNavMode.Value = useNavMode;
            prefWindowSize.Value = windowSizeMultiplier;
            prefZoom.Value = mapZoom;
            prefOpacity.Value = mapOpacity;
            prefArrowSize.Value = arrowSize;
            prefArrowR.Value = arrowR;
            prefArrowG.Value = arrowG;
            prefArrowB.Value = arrowB;

            prefCategory.SaveToFile();
            HandleSettingsUpdate();
        }

        public override void OnGUI()
        {
            if (!guiVisible) return;

            if (solidColorTex == null)
            {
                solidColorTex = new Texture2D(1, 1);
                solidColorTex.SetPixel(0, 0, Color.white);
                solidColorTex.Apply();
            }

            float currentMapSize = 150f * windowSizeMultiplier;
            float menuY = 20f + currentMapSize + 60f;

            Rect menuRect = new Rect(Screen.width - 250, menuY, 230, 585);
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            GUI.DrawTexture(menuRect, solidColorTex);
            GUI.color = Color.white;

            GUI.Label(new Rect(menuRect.x + 10, menuRect.y + 5, 210, 20), "<b>Skoofidon's Minimap</b>");

            DrawToggle(new Rect(menuRect.x + 10, menuRect.y + 30, 210, 25), "Minimap Enabled", ref minimapEnabled, SaveSettingsFromF3);
            DrawToggle(new Rect(menuRect.x + 10, menuRect.y + 60, 210, 25), "Time Display", ref timeBarEnabled, SaveSettingsFromF3);
            DrawToggle(new Rect(menuRect.x + 10, menuRect.y + 90, 210, 25), "Show Orders", ref showOrdersEnabled, SaveSettingsFromF3);
            DrawToggle(new Rect(menuRect.x + 10, menuRect.y + 120, 210, 25), "Show Dealers", ref showDealersEnabled, SaveSettingsFromF3);
            DrawToggle(new Rect(menuRect.x + 10, menuRect.y + 150, 210, 25), "Show Customers", ref showCustomersEnabled, SaveSettingsFromF3);
            DrawToggle(new Rect(menuRect.x + 10, menuRect.y + 180, 210, 25), "Show Dead Drops", ref showDeadDropsEnabled, SaveSettingsFromF3);
            DrawToggle(new Rect(menuRect.x + 10, menuRect.y + 210, 210, 25), "Show Co-op Radar", ref coopRadarEnabled, SaveSettingsFromF3);
            DrawToggle(new Rect(menuRect.x + 10, menuRect.y + 240, 210, 25), "Navigation Mode", ref useNavMode, SaveSettingsFromF3);
            DrawToggle(new Rect(menuRect.x + 10, menuRect.y + 270, 210, 25), "Show Properties", ref showPropertiesEnabled, SaveSettingsFromF3);
            DrawToggle(new Rect(menuRect.x + 10, menuRect.y + 300, 210, 25), "Show Vehicles", ref showVehiclesEnabled, SaveSettingsFromF3);

            GUI.Label(new Rect(menuRect.x + 10, menuRect.y + 330, 210, 20), "Window Size: " + windowSizeMultiplier.ToString("F1") + "x");
            float newWinSize = GUI.HorizontalSlider(new Rect(menuRect.x + 10, menuRect.y + 350, 210, 20), windowSizeMultiplier, 1f, 3f);
            if (Mathf.Abs(newWinSize - windowSizeMultiplier) > 0.01f) { windowSizeMultiplier = newWinSize; SaveSettingsFromF3(); }

            GUI.Label(new Rect(menuRect.x + 10, menuRect.y + 370, 210, 20), "Map Zoom: " + mapZoom.ToString("F1") + "x");
            float newZoom = GUI.HorizontalSlider(new Rect(menuRect.x + 10, menuRect.y + 390, 210, 20), mapZoom, 0.5f, 3f);
            if (Mathf.Abs(newZoom - mapZoom) > 0.01f) { mapZoom = newZoom; SaveSettingsFromF3(); }

            GUI.Label(new Rect(menuRect.x + 10, menuRect.y + 410, 210, 20), "Opacity: " + Mathf.RoundToInt(mapOpacity * 100) + "%");
            float newOpacity = GUI.HorizontalSlider(new Rect(menuRect.x + 10, menuRect.y + 430, 210, 20), mapOpacity, 0.2f, 1f);
            if (Mathf.Abs(newOpacity - mapOpacity) > 0.01f) { mapOpacity = newOpacity; SaveSettingsFromF3(); }

            GUI.Label(new Rect(menuRect.x + 10, menuRect.y + 450, 210, 20), "Arrow Size: " + arrowSize.ToString("F1") + "x");
            float newASize = GUI.HorizontalSlider(new Rect(menuRect.x + 10, menuRect.y + 470, 210, 20), arrowSize, 0.5f, 3f);
            if (Mathf.Abs(newASize - arrowSize) > 0.01f) { arrowSize = newASize; SaveSettingsFromF3(); }

            GUI.Label(new Rect(menuRect.x + 10, menuRect.y + 490, 100, 20), "Arrow Color");
            GUI.color = new Color(arrowR, arrowG, arrowB);
            GUI.DrawTexture(new Rect(menuRect.x + 150, menuRect.y + 490, 70, 20), solidColorTex);
            GUI.color = Color.white;

            GUI.Label(new Rect(menuRect.x + 10, menuRect.y + 515, 20, 20), "R");
            float newR = GUI.HorizontalSlider(new Rect(menuRect.x + 30, menuRect.y + 520, 190, 20), arrowR, 0f, 1f);
            if (Mathf.Abs(newR - arrowR) > 0.01f) { arrowR = newR; SaveSettingsFromF3(); }

            GUI.Label(new Rect(menuRect.x + 10, menuRect.y + 535, 20, 20), "G");
            float newG = GUI.HorizontalSlider(new Rect(menuRect.x + 30, menuRect.y + 540, 190, 20), arrowG, 0f, 1f);
            if (Mathf.Abs(newG - arrowG) > 0.01f) { arrowG = newG; SaveSettingsFromF3(); }

            GUI.Label(new Rect(menuRect.x + 10, menuRect.y + 555, 20, 20), "B");
            float newB = GUI.HorizontalSlider(new Rect(menuRect.x + 30, menuRect.y + 560, 190, 20), arrowB, 0f, 1f);
            if (Mathf.Abs(newB - arrowB) > 0.01f) { arrowB = newB; SaveSettingsFromF3(); }
        }

        private void DrawToggle(Rect position, string label, ref bool state, Action onToggle)
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(position.x + 40f, position.y, position.width - 40f, position.height), label);

            Rect boxRect = new Rect(position.x, position.y + 4f, 32f, 16f);
            GUI.color = state ? new Color(0.2f, 0.8f, 0.2f, 1f) : new Color(0.8f, 0.2f, 0.2f, 1f);
            GUI.DrawTexture(boxRect, solidColorTex);

            Rect knobRect = new Rect(state ? boxRect.x + 18f : boxRect.x + 2f, boxRect.y + 2f, 12f, 12f);
            GUI.color = Color.white;
            GUI.DrawTexture(knobRect, solidColorTex);

            if (GUI.Button(position, GUIContent.none, GUIStyle.none))
            {
                state = !state;
                onToggle();
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                if (sceneName == "Main" && !isInitializing)
                {
                    isInitializing = true;
                    if (minimapObject != null)
                    {
                        UnityEngine.Object.Destroy(minimapObject);
                        minimapObject = null;
                        cachedContentRt = null;
                        cachedRotatorRt = null;
                        cachedMinimapMask = null;
                        cachedLocalPlayerUI = null;
                        activeCoopPlayerRts.Clear();
                    }

                    CreateMinimapUI();
                    UpdateMinimapSize();
                    UpdateMinimapOpacity();

                    MelonCoroutines.Start(FindGameObjectsRoutine());
                    MelonCoroutines.Start(UpdateMinimapTimeCoroutine());
                    MelonCoroutines.Start(POISyncCoroutine());

                    isInitializing = false;
                    hasLoggedUpdateError = false;
                    isEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Failed to initialize Skoofidon's Minimap: " + ex.Message);
                isInitializing = false;
            }
        }

        public override void OnUpdate()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.F3))
                {
                    guiVisible = !guiVisible;
                }

                if (Input.GetKeyDown(KeyCode.F4))
                {
                    RunScanner();
                }

                if (!isEnabled || playerObject == null || minimapDisplayObject == null || cachedContentRt == null) return;

                Vector3 position = playerObject.transform.position;
                Vector2 baseOffset = new Vector2(-position.x * mapScale + 9f, -position.z * mapScale - 1f);
                Vector2 targetPos = baseOffset * mapZoom;

                cachedContentRt.anchoredPosition = Vector2.Lerp(cachedContentRt.anchoredPosition, targetPos, Time.deltaTime * smoothingFactor);

                if (cachedRotatorRt != null)
                {
                    float targetRot = useNavMode ? playerObject.transform.rotation.eulerAngles.y : 0f;
                    float currentRot = cachedRotatorRt.localEulerAngles.z;
                    float newRot = Mathf.LerpAngle(currentRot, targetRot, Time.deltaTime * smoothingFactor);
                    cachedRotatorRt.localEulerAngles = new Vector3(0, 0, newRot);
                }

                UpdatePlayerArrow();
                UpdateCoopMarkers();
            }
            catch (Exception ex)
            {
                if (!hasLoggedUpdateError)
                {
                    MelonLogger.Error($"Critical error in OnUpdate (Minimap will stop updating): {ex}");
                    hasLoggedUpdateError = true;
                    isEnabled = false;
                }
            }
        }

        private void RunScanner()
        {
            MelonLogger.Msg("============== SCAN START ==============");
            if (cachedMapContent != null)
            {
                MelonLogger.Msg("--- UI MARKERS ---");
                for (int i = 0; i < cachedMapContent.transform.childCount; i++)
                {
                    MelonLogger.Msg("UI Element: " + cachedMapContent.transform.GetChild(i).name);
                }
            }

            if (playerObject != null)
            {
                MelonLogger.Msg("--- 3D OBJECTS WITHIN 5 METERS ---");
                GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                HashSet<string> loggedRoots = new HashSet<string>();

                foreach (GameObject obj in allObjects)
                {
                    if (obj.activeInHierarchy)
                    {
                        float dist = Vector3.Distance(playerObject.transform.position, obj.transform.position);
                        if (dist <= 5f)
                        {
                            Transform root = obj.transform.root;
                            if (root != playerObject.transform.root && !loggedRoots.Contains(root.name))
                            {
                                MelonLogger.Msg($"Found 3D: [{root.name}] | Path: {GetGameObjectPath(root)}");
                                loggedRoots.Add(root.name);
                            }
                        }
                    }
                }
            }
            MelonLogger.Msg("=============== SCAN END ===============");
        }

        private string GetGameObjectPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        private IEnumerator POISyncCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

                if (cachedMapContent == null || mapContentObject == null || !minimapEnabled) continue;

                currentKeysBuffer.Clear();
                currentKeysBuffer.AddRange(syncedPOIs.Keys);
                validKeysBuffer.Clear();
                activeCoopPlayerRts.Clear();
                cachedLocalPlayerUI = null;

                RectTransform origContentRt = cachedMapContent.GetComponent<RectTransform>();
                if (origContentRt == null || origContentRt.rect.width == 0) continue;

                float ratioX = 500f / origContentRt.rect.width;
                float ratioY = 500f / origContentRt.rect.height;

                bool foundLocalPlayer = false;

                for (int i = 0; i < cachedMapContent.transform.childCount; i++)
                {
                    Transform child = cachedMapContent.transform.GetChild(i);
                    string childName = child.name;

                    if (childName.Contains("PlayerPoI(Clone)"))
                    {
                        if (!foundLocalPlayer)
                        {
                            foundLocalPlayer = true;
                            cachedLocalPlayerUI = child.GetComponent<RectTransform>(); // Сохраняем локального игрока
                        }
                        else
                        {
                            RectTransform pRt = child.GetComponent<RectTransform>();
                            if (pRt != null) activeCoopPlayerRts.Add(pRt);
                        }
                        continue;
                    }

                    if (childName.Contains("PlayerPoI")) continue;

                    if (!showOrdersEnabled && childName.Contains("ContractPoI")) continue;
                    if (!showDealersEnabled && childName.Contains("NPCPoI")) continue;
                    if (!showCustomersEnabled && childName.Contains("PotentialCustomerPoI")) continue;
                    if (!showDeadDropsEnabled && childName.Contains("DeaddropPoI")) continue;
                    if (!showPropertiesEnabled && childName.Contains("PropertyPoI")) continue;
                    if (!showVehiclesEnabled && childName.Contains("OwnedVehiclePoI")) continue;

                    RectTransform childRt = child.GetComponent<RectTransform>();
                    if (childRt == null || childRt.rect.width > 150f || childRt.rect.height > 150f) continue;

                    validKeysBuffer.Add(child);

                    if (!syncedPOIs.ContainsKey(child))
                    {
                        Transform iconToClone = child.Find("IconContainer");
                        if (iconToClone == null) iconToClone = child;

                        GameObject clone = UnityEngine.Object.Instantiate(iconToClone.gameObject);
                        clone.name = "MinimapPOI_" + childName;
                        clone.transform.SetParent(mapContentObject.transform, false);

                        RectTransform cloneRt = clone.GetComponent<RectTransform>();
                        if (cloneRt != null) cloneRt.localScale = new Vector3(0.5f, 0.5f, 0.5f);

                        syncedPOIs.Add(child, clone);
                    }

                    GameObject myMarker = syncedPOIs[child];
                    if (myMarker != null)
                    {
                        RectTransform myRt = myMarker.GetComponent<RectTransform>();
                        if (childRt != null && myRt != null)
                        {
                            myRt.anchorMin = childRt.anchorMin;
                            myRt.anchorMax = childRt.anchorMax;
                            myRt.pivot = childRt.pivot;
                            myRt.anchoredPosition = new Vector2(childRt.anchoredPosition.x * ratioX, childRt.anchoredPosition.y * ratioY);
                        }
                    }
                }

                foreach (Transform key in currentKeysBuffer)
                {
                    if (!validKeysBuffer.Contains(key))
                    {
                        if (syncedPOIs[key] != null) UnityEngine.Object.Destroy(syncedPOIs[key]);
                        syncedPOIs.Remove(key);
                    }
                }
            }
        }

        private void UpdateCoopMarkers()
        {
            if (!coopRadarEnabled || cachedMapContent == null || playerObject == null || minimapDisplayObject == null || cachedMinimapMask == null || cachedLocalPlayerUI == null)
            {
                ClearCoopMarkers();
                return;
            }

            RectTransform origContentRt = cachedMapContent.GetComponent<RectTransform>();
            if (origContentRt == null || origContentRt.rect.width == 0) return;

            float ratioX = 500f / origContentRt.rect.width;
            float ratioY = 500f / origContentRt.rect.height;
            float minimapRadius = 70f * windowSizeMultiplier - 5f;
            int markerIndex = 0;

            foreach (RectTransform rt in activeCoopPlayerRts)
            {
                if (rt == null) continue;

                if (markerIndex >= coopMarkers.Count)
                {
                    GameObject marker = new GameObject("CoopMarker_" + markerIndex);
                    marker.transform.SetParent(cachedMinimapMask, false);
                    RectTransform mrt = marker.AddComponent<RectTransform>();
                    mrt.sizeDelta = new Vector2(10f, 10f);
                    mrt.anchorMin = new Vector2(0.5f, 0.5f);
                    mrt.anchorMax = new Vector2(0.5f, 0.5f);
                    mrt.pivot = new Vector2(0.5f, 0.5f);

                    Image img = marker.AddComponent<Image>();
                    img.sprite = CreateCircleSprite(10, Color.green);
                    img.color = Color.green;
                    coopMarkers.Add(marker);
                }

                GameObject currentMarker = coopMarkers[markerIndex];
                currentMarker.SetActive(true);
                RectTransform markerRt = currentMarker.GetComponent<RectTransform>();

                // Считаем дистанцию между иконками прямо в UI пространства оригинальной карты
                Vector2 rawOffset = rt.anchoredPosition - cachedLocalPlayerUI.anchoredPosition;

                // Сжимаем её до размеров мини-карты и применяем зум
                Vector2 offset = new Vector2(rawOffset.x * ratioX, rawOffset.y * ratioY) * mapZoom;

                if (useNavMode)
                {
                    float angle = playerObject.transform.rotation.eulerAngles.y * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);
                    offset = new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
                }

                if (offset.magnitude > minimapRadius)
                {
                    markerRt.anchoredPosition = offset.normalized * minimapRadius;
                    currentMarker.GetComponent<Image>().color = new Color(0f, 1f, 0f, 0.4f);
                }
                else
                {
                    markerRt.anchoredPosition = offset;
                    currentMarker.GetComponent<Image>().color = new Color(0f, 1f, 0f, 1f);
                }

                markerIndex++;
            }

            for (int i = markerIndex; i < coopMarkers.Count; i++)
            {
                if (coopMarkers[i] != null) coopMarkers[i].SetActive(false);
            }
        }

        private void ClearCoopMarkers()
        {
            foreach (var m in coopMarkers) if (m != null) m.SetActive(false);
        }

        private void UpdateMinimapSize()
        {
            float size = 150f * windowSizeMultiplier;
            float maskSize = 140f * windowSizeMultiplier;

            if (minimapFrameRect != null) minimapFrameRect.sizeDelta = new Vector2(size, size);

            if (minimapDisplayObject != null)
            {
                RectTransform rt = minimapDisplayObject.GetComponent<RectTransform>();
                if (rt != null) rt.offsetMin = new Vector2(0f, 50f);
            }

            if (minimapDisplayObject != null)
            {
                Transform mask = minimapDisplayObject.transform.Find("MinimapMask");
                if (mask != null) mask.GetComponent<RectTransform>().sizeDelta = new Vector2(maskSize, maskSize);

                Transform border = minimapDisplayObject.transform.Find("MinimapBorder");
                if (border != null) border.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);
            }
        }

        private void UpdateMinimapOpacity()
        {
            if (minimapDisplayObject == null) return;

            Transform mask = minimapDisplayObject.transform.Find("MinimapMask");
            if (mask != null)
            {
                Image img = mask.GetComponent<Image>();
                if (img != null) { Color c = img.color; c.a = mapOpacity * 0.8f; img.color = c; }
            }

            Transform border = minimapDisplayObject.transform.Find("MinimapBorder");
            if (border != null)
            {
                Image img = border.GetComponent<Image>();
                if (img != null) { Color c = img.color; c.a = mapOpacity; img.color = c; }
            }

            if (mapContentObject != null)
            {
                Image img = mapContentObject.GetComponent<Image>();
                if (img != null) { Color c = img.color; c.a = mapOpacity; img.color = c; }
            }
        }

        private IEnumerator FindGameObjectsRoutine()
        {
            yield return new WaitForSeconds(2f);
            int attempts = 0;

            while ((mapAppObject == null || playerObject == null) && attempts < 30)
            {
                attempts++;
                if (playerObject == null) playerObject = GameObject.Find("Player_Local");

                if (mapAppObject == null)
                {
                    GameObject gameplayMenu = GameObject.Find("GameplayMenu");
                    if (gameplayMenu != null)
                    {
                        Transform phone = gameplayMenu.transform.Find("Phone");
                        if (phone != null)
                        {
                            Transform phoneChild = phone.Find("phone");
                            if (phoneChild != null)
                            {
                                Transform appsCanvas = phoneChild.Find("AppsCanvas");
                                if (appsCanvas != null)
                                {
                                    Transform mapApp = appsCanvas.Find("MapApp");
                                    if (mapApp != null) mapAppObject = mapApp.gameObject;
                                }
                            }
                        }
                    }
                }

                if (mapAppObject != null && viewportObject == null)
                {
                    Transform container = mapAppObject.transform.Find("Container");
                    if (container != null)
                    {
                        Transform scrollView = container.Find("Scroll View");
                        if (scrollView != null)
                        {
                            Transform viewport = scrollView.Find("Viewport");
                            if (viewport != null) viewportObject = viewport.gameObject;
                        }
                    }
                }

                if (mapAppObject == null || playerObject == null) yield return new WaitForSeconds(0.5f);
            }

            if (viewportObject != null && viewportObject.transform.childCount > 0)
            {
                Transform contentTransform = viewportObject.transform.GetChild(0);
                Sprite foundSprite = null;

                Image contentImage = contentTransform.GetComponent<Image>();
                if (contentImage != null && contentImage.sprite != null)
                {
                    foundSprite = contentImage.sprite;
                }
                else
                {
                    for (int i = 0; i < contentTransform.childCount; i++)
                    {
                        Image childImage = contentTransform.GetChild(i).GetComponent<Image>();
                        if (childImage != null && childImage.sprite != null)
                        {
                            foundSprite = childImage.sprite;
                            break;
                        }
                    }
                }

                if (foundSprite != null && mapContentObject != null)
                {
                    Image minimapImage = mapContentObject.GetComponent<Image>();
                    if (minimapImage == null) minimapImage = mapContentObject.AddComponent<Image>();

                    minimapImage.sprite = foundSprite;
                    minimapImage.color = Color.white;
                    minimapImage.type = Image.Type.Simple;
                    minimapImage.preserveAspect = true;
                    minimapImage.enabled = true;

                    if (gridContainer != null) gridContainer.gameObject.SetActive(false);
                }

                UpdateMinimapOpacity();
            }

            CreateVisualGrid();

            if (cachedMapContent == null)
                cachedMapContent = GameObject.Find("GameplayMenu/Phone/phone/AppsCanvas/MapApp/Container/Scroll View/Viewport/Content");
        }

        private IEnumerator UpdateMinimapTimeCoroutine()
        {
            while (true)
            {
                if (cachedGameTimeText == null)
                {
                    GameObject val = GameObject.Find("GameplayMenu/Phone/phone/HomeScreen/InfoBar/Time");
                    if (val != null) cachedGameTimeText = val.GetComponent<Text>();
                }

                if (cachedGameTimeText != null && minimapTimeText != null)
                {
                    string text = cachedGameTimeText.text;
                    string[] array = text.Split(spaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                    if (array.Length >= 3)
                        minimapTimeText.text = array[array.Length - 1] + "\n" + array[0] + " " + array[1];
                    else
                        minimapTimeText.text = text;
                }
                yield return new WaitForSeconds(1f);
            }
        }

        private void CreateMinimapUI()
        {
            minimapObject = new GameObject("MinimapContainer");
            UnityEngine.Object.DontDestroyOnLoad(minimapObject);

            GameObject canvasObj = new GameObject("MinimapCanvas");
            canvasObj.transform.SetParent(minimapObject.transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject frameObj = new GameObject("MinimapFrame");
            frameObj.transform.SetParent(canvasObj.transform, false);
            minimapFrameRect = frameObj.AddComponent<RectTransform>();
            minimapFrameRect.anchorMin = new Vector2(1f, 1f);
            minimapFrameRect.anchorMax = new Vector2(1f, 1f);
            minimapFrameRect.pivot = new Vector2(1f, 1f);
            minimapFrameRect.anchoredPosition = new Vector2(-20f, -20f);

            minimapDisplayObject = new GameObject("MinimapDisplay");
            minimapDisplayObject.transform.SetParent(frameObj.transform, false);
            RectTransform displayRt = minimapDisplayObject.AddComponent<RectTransform>();
            displayRt.anchorMin = new Vector2(0f, 0f);
            displayRt.anchorMax = new Vector2(1f, 1f);
            displayRt.offsetMax = Vector2.zero;

            GameObject maskObj = new GameObject("MinimapMask");
            maskObj.transform.SetParent(minimapDisplayObject.transform, false);
            RectTransform maskRt = maskObj.AddComponent<RectTransform>();
            maskRt.anchorMin = new Vector2(0.5f, 0.5f);
            maskRt.anchorMax = new Vector2(0.5f, 0.5f);
            maskRt.pivot = new Vector2(0.5f, 0.5f);
            maskRt.anchoredPosition = Vector2.zero;

            cachedMinimapMask = maskObj.transform;

            Mask mask = maskObj.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            Image maskImg = maskObj.AddComponent<Image>();
            maskImg.sprite = CreateCircleSprite(140, new Color(0.1f, 0.1f, 0.1f, 1f));

            GameObject borderObj = new GameObject("MinimapBorder");
            borderObj.transform.SetParent(minimapDisplayObject.transform, false);
            RectTransform borderRt = borderObj.AddComponent<RectTransform>();
            borderRt.anchorMin = new Vector2(0.5f, 0.5f);
            borderRt.anchorMax = new Vector2(0.5f, 0.5f);
            borderRt.pivot = new Vector2(0.5f, 0.5f);
            borderRt.anchoredPosition = Vector2.zero;
            borderObj.transform.SetSiblingIndex(0);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.sprite = CreateCircleSprite(150, Color.black);

            GameObject rotatorObj = new GameObject("MapRotator");
            rotatorObj.transform.SetParent(maskObj.transform, false);
            cachedRotatorRt = rotatorObj.AddComponent<RectTransform>();
            cachedRotatorRt.anchorMin = new Vector2(0.5f, 0.5f);
            cachedRotatorRt.anchorMax = new Vector2(0.5f, 0.5f);
            cachedRotatorRt.pivot = new Vector2(0.5f, 0.5f);
            cachedRotatorRt.anchoredPosition = Vector2.zero;

            mapContentObject = new GameObject("MapContent");
            mapContentObject.transform.SetParent(rotatorObj.transform, false);
            cachedContentRt = mapContentObject.AddComponent<RectTransform>();
            cachedContentRt.sizeDelta = new Vector2(500f, 500f);
            cachedContentRt.anchorMin = new Vector2(0.5f, 0.5f);
            cachedContentRt.anchorMax = new Vector2(0.5f, 0.5f);
            cachedContentRt.pivot = new Vector2(0.5f, 0.5f);
            cachedContentRt.anchoredPosition = Vector2.zero;

            cachedContentRt.localScale = new Vector3(mapZoom, mapZoom, 1f);

            GameObject gridObj = new GameObject("GridContainer");
            gridObj.transform.SetParent(mapContentObject.transform, false);
            gridContainer = gridObj.AddComponent<RectTransform>();
            gridContainer.sizeDelta = new Vector2(500f, 500f);
            gridContainer.anchorMin = new Vector2(0.5f, 0.5f);
            gridContainer.anchorMax = new Vector2(0.5f, 0.5f);
            gridContainer.pivot = new Vector2(0.5f, 0.5f);
            gridContainer.anchoredPosition = Vector2.zero;

            CreateMinimapTimeDisplay(minimapFrameRect);
            CreateFallbackPlayerMarker(maskObj);

            minimapDisplayObject.SetActive(minimapEnabled);
        }

        private void CreateMinimapTimeDisplay(Transform parent)
        {
            GameObject val = new GameObject("MinimapTimeContainer");
            val.transform.SetParent(parent, false);
            minimapTimeContainer = val.AddComponent<RectTransform>();
            minimapTimeContainer.sizeDelta = new Vector2(80f, 40f);
            minimapTimeContainer.anchorMin = new Vector2(0.5f, 0f);
            minimapTimeContainer.anchorMax = new Vector2(0.5f, 0f);
            minimapTimeContainer.pivot = new Vector2(0.5f, 1f);
            minimapTimeContainer.anchoredPosition = new Vector2(0f, -5f);

            Image val3 = val.AddComponent<Image>();
            val3.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);

            GameObject val4 = new GameObject("MinimapTime");
            val4.transform.SetParent(val.transform, false);
            RectTransform val5 = val4.AddComponent<RectTransform>();
            val5.anchorMin = Vector2.zero;
            val5.anchorMax = Vector2.one;
            val5.offsetMin = Vector2.zero;
            val5.offsetMax = Vector2.zero;

            minimapTimeText = val4.AddComponent<Text>();
            minimapTimeText.text = "Time";
            minimapTimeText.alignment = TextAnchor.MiddleCenter;
            minimapTimeText.color = Color.white;
            minimapTimeText.fontSize = 12;
            minimapTimeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            minimapTimeContainer.gameObject.SetActive(minimapEnabled && timeBarEnabled);
        }

        private void UpdatePlayerArrow()
        {
            if (cachedPlayerMarker == null || playerObject == null) return;

            float targetRotationZ = useNavMode ? 0f : -playerObject.transform.rotation.eulerAngles.y;
            cachedPlayerMarker.transform.localRotation = Quaternion.Euler(0, 0, targetRotationZ);
        }

        private Sprite CreateArrowSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                    tex.SetPixel(i, j, clear);

            int centerX = size / 2;
            for (int y = 0; y < size; y++)
            {
                float t = (float)y / size;
                int width = Mathf.RoundToInt((1f - t) * (size / 2f));

                if (y < size * 0.3f)
                {
                    float innerWidth = Mathf.RoundToInt(((size * 0.3f - y) / (size * 0.3f)) * (size / 2f));
                    for (int x = centerX - width; x <= centerX + width; x++)
                    {
                        if (x < centerX - innerWidth || x > centerX + innerWidth)
                            tex.SetPixel(x, y, Color.white);
                    }
                }
                else
                {
                    for (int x = centerX - width; x <= centerX + width; x++)
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateCircleSprite(int diameter, Color color)
        {
            Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            for (int i = 0; i < diameter; i++)
                for (int j = 0; j < diameter; j++)
                    tex.SetPixel(j, i, clear);

            int radius = diameter / 2;
            Vector2 center = new Vector2(radius, radius);
            for (int k = 0; k < diameter; k++)
            {
                for (int l = 0; l < diameter; l++)
                {
                    if (Vector2.Distance(new Vector2(l, k), center) <= radius)
                        tex.SetPixel(l, k, color);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f));
        }

        private void CreateFallbackPlayerMarker(GameObject parent)
        {
            cachedPlayerMarker = new GameObject("PlayerMarkerArrow");
            cachedPlayerMarker.transform.SetParent(parent.transform, false);
            RectTransform val = cachedPlayerMarker.AddComponent<RectTransform>();
            val.sizeDelta = new Vector2(16f, 16f);
            val.anchorMin = new Vector2(0.5f, 0.5f);
            val.anchorMax = new Vector2(0.5f, 0.5f);
            val.pivot = new Vector2(0.5f, 0.5f);
            val.anchoredPosition = Vector2.zero;

            Image val2 = cachedPlayerMarker.AddComponent<Image>();
            val2.sprite = CreateArrowSprite(32);
            val2.color = new Color(arrowR, arrowG, arrowB, 1f);

            val.localScale = new Vector3(arrowSize, arrowSize, 1f);
        }

        private void CreateVisualGrid()
        {
            try
            {
                if (gridContainer != null)
                {
                    int num = 10;
                    for (int i = 0; i <= num; i++)
                    {
                        GameObject val = new GameObject($"HLine_{i}");
                        val.transform.SetParent(gridContainer, false);
                        RectTransform val2 = val.AddComponent<RectTransform>();
                        val2.sizeDelta = new Vector2((float)(num * gridSize), 1f);
                        val2.anchorMin = new Vector2(0.5f, 0.5f);
                        val2.anchorMax = new Vector2(0.5f, 0.5f);
                        val2.pivot = new Vector2(0.5f, 0.5f);
                        val2.anchoredPosition = new Vector2(0f, (i - num / 2) * gridSize);
                        Image val3 = val.AddComponent<Image>();
                        val3.color = gridColor;
                    }
                    for (int j = 0; j <= num; j++)
                    {
                        GameObject val4 = new GameObject($"VLine_{j}");
                        val4.transform.SetParent(gridContainer, false);
                        RectTransform val5 = val4.AddComponent<RectTransform>();
                        val5.sizeDelta = new Vector2(1f, (float)(num * gridSize));
                        val5.anchorMin = new Vector2(0.5f, 0.5f);
                        val5.anchorMax = new Vector2(0.5f, 0.5f);
                        val5.pivot = new Vector2(0.5f, 0.5f);
                        val5.anchoredPosition = new Vector2((j - num / 2) * gridSize, 0f);
                        Image val6 = val4.AddComponent<Image>();
                        val6.color = gridColor;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Error creating visual grid: " + ex.Message);
            }
        }
    }
}