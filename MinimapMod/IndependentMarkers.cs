using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.PlayerScripts;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace MinimapMod
{
    public partial class MainMod
    {
        private const int DealerCategory = 1;
        private const int CustomerCategory = 2;
        private const int CoopCategory = 4;
        // Soft CPU budget: a native call cannot be interrupted mid-call. There is
        // also a hard work-count limit and at most one new icon per frame.
        private const double TrackingBudgetSeconds = 0.00035;
        private const int NpcChecksPerFrame = 8;
        private const int CoopChecksPerFrame = 4;

        private sealed class WorldMarker : ITrackingView
        {
            public readonly MarkerBindingState Binding = new MarkerBindingState();
            public long Id;
            public NPCPoI Poi;
            public NPC Npc;
            public Player Player;
            public GameObject SourceObject;
            public Transform Target;
            public RectTransform View;
            public Image Image;
            public RectTransform DealerBorderOuter;
            public RectTransform DealerBorderClip;
            public int Category;
            public int SeenGeneration;
            public Vector3 Position;
            public bool HasPosition;

            public void Hide() { SetMarkerVisible(View, false); }
            public void Invalidate()
            {
                if (View != null) UnityEngine.Object.Destroy(View.gameObject);
                View = null;
                Image = null;
                DealerBorderOuter = DealerBorderClip = null;
                HasPosition = false;
            }
        }

        private static MainMod independentMarkerOwner;
        private readonly Dictionary<long, WorldMarker> npcWorldMarkers = new Dictionary<long, WorldMarker>();
        private readonly Dictionary<long, WorldMarker> coopWorldMarkers = new Dictionary<long, WorldMarker>();
        private readonly TrackingQueue<WorldMarker> npcWork = new TrackingQueue<WorldMarker>();
        private readonly TrackingQueue<WorldMarker> coopWork = new TrackingQueue<WorldMarker>();
        private readonly List<long> staleWorldIds = new List<long>();
        private readonly Stack<RectTransform> unusedCoopDots = new Stack<RectTransform>();
        private Il2CppSystem.Collections.Generic.List<Player> playerRegistry;
        private int playerRegistryIndex;
        private int playerGeneration;
        private bool reconcilingPlayers;
        private double nextPlayerReconcile;
        private double nextProjectionAttempt;
        private double nextTrackingWarning;
        private bool npcBindingHookReady;
        private bool projectionReady;
        private MapProjection worldProjection;
        private Sprite coopDotSprite;
        private Canvas dealerBorderCanvas;
        private float lastDealerCanvasScale = -1;
        private long trackingFrames, trackingTicks, worstTrackingTicks, npcSamples, coopSamples, budgetExits;

        private void InitializeIndependentMarkers()
        {
            independentMarkerOwner = this;
            try
            {
                HarmonyInstance.Patch(AccessTools.Method(typeof(NPCPoI), nameof(NPCPoI.SetNPC)),
                    postfix: new HarmonyMethod(typeof(MainMod), nameof(NpcBoundToPoi)));
                npcBindingHookReady = true;
            }
            catch (Exception ex)
            {
                // A failed optional adapter must not prevent F3 or the map from loading.
                MelonLogger.Warning("Independent NPC binding unavailable; retaining phone NPC markers: " + ex.Message);
            }
        }

        private static void NpcBoundToPoi(NPCPoI __instance, NPC npc)
        {
            MainMod owner = independentMarkerOwner;
            if (owner == null) return;
            try { owner.RegisterNpc(__instance, npc); }
            catch (Exception ex) { owner.WarnAboutTracking(ex); }
        }

        private void RegisterNpc(NPCPoI poi, NPC npc)
        {
            if (poi == null || npc == null) return;
            long id = poi.Pointer.ToInt64();
            if (!npcWorldMarkers.TryGetValue(id, out WorldMarker marker))
            {
                marker = new WorldMarker { Id = id };
                npcWorldMarkers.Add(id, marker);
            }
            marker.Poi = poi;
            marker.Npc = npc;
            // These are world POIs, not their phone UI transforms. The game's
            // POI.UpdatePosition uses this same world Transform.
            marker.Target = poi.transform;
            marker.SourceObject = poi.gameObject;
            GameObject prefab = poi.UIPrefab;
            marker.Category = prefab != null && prefab.name.Contains("PotentialCustomerPoI")
                ? CustomerCategory : DealerCategory;
            marker.Binding.Bind(npc.Pointer.ToInt64(), marker.Category, EnabledWorldCategories, marker);
            npcWork.Upsert(id, marker, marker.Category, 0.1);
        }

        private int EnabledWorldCategories => (showDealersEnabled ? DealerCategory : 0)
            | (showCustomersEnabled ? CustomerCategory : 0) | (coopRadarEnabled ? CoopCategory : 0);

        private static bool TrackingBudgetExpired(long started)
        {
            return Stopwatch.GetTimestamp() - started >= Stopwatch.Frequency * TrackingBudgetSeconds;
        }

        private void UpdateIndependentMarkers(Vector3 localPosition, float playerYaw)
        {
            double now = Time.realtimeSinceStartup;
            long started = Stopwatch.GetTimestamp();
            int iconsCreated = 0;
            int categories = EnabledWorldCategories;
            try
            {
                if (!projectionReady && now >= nextProjectionAttempt)
                {
                    nextProjectionAttempt = now + 0.5;
                    TryInitializeWorldProjection();
                }
                if (coopRadarEnabled && !TrackingBudgetExpired(started))
                {
                    try { ReconcilePlayers(now, started); }
                    catch (Exception ex)
                    {
                        reconcilingPlayers = false;
                        playerRegistry = null;
                        nextPlayerReconcile = now + 1;
                        WarnAboutTracking(ex);
                    }
                }

                coopWork.BeginFrame(CoopChecksPerFrame);
                while (!TrackingBudgetExpired(started) && coopWork.TryTake(now, categories, out WorldMarker marker))
                {
                    try
                    {
                        if (marker.Player == null || marker.Target == null)
                        {
                            RemoveCoopMarker(marker.Id);
                            continue;
                        }
                        if (marker.View == null)
                        {
                            if (iconsCreated != 0) continue;
                            marker.View = AcquireCoopDot();
                            marker.Image = marker.View.GetComponent<Image>();
                            iconsCreated++;
                        }
                        marker.Position = marker.Target.position;
                        marker.HasPosition = true;
                        coopSamples++;
                    }
                    catch (Exception ex) { RemoveCoopMarker(marker.Id); WarnAboutTracking(ex); }
                }

                npcWork.BeginFrame(NpcChecksPerFrame);
                while (projectionReady && !TrackingBudgetExpired(started)
                    && npcWork.TryTake(now, categories, out WorldMarker marker))
                {
                    try
                    {
                        if (!marker.Binding.CanSample(now, categories)) continue;
                        if (marker.Poi == null || marker.Npc == null || marker.Target == null)
                        {
                            RemoveNpcMarker(marker.Id);
                            continue;
                        }
                        // The source is in the world; its visibility does not depend
                        // on any phone app's activeInHierarchy state.
                        bool visible = marker.Poi.enabled && marker.SourceObject.activeInHierarchy;
                        if (!visible) { SetMarkerVisible(marker.View, false); continue; }
                        if (marker.View == null)
                        {
                            if (iconsCreated != 0) continue;
                            marker.View = CreateNpcPortrait(marker);
                            iconsCreated++;
                            if (marker.View == null) { marker.Binding.Failed(now); continue; }
                        }
                        Vector3 position = marker.Target.position;
                        worldProjection.Project(position.x, position.z, out float x, out float y);
                        Vector2 mapPosition = new Vector2(x, y);
                        if (marker.View.anchoredPosition != mapPosition) marker.View.anchoredPosition = mapPosition;
                        SetMarkerVisible(marker.View, true);
                        marker.Binding.Succeeded();
                        npcSamples++;
                    }
                    catch (Exception ex)
                    {
                        // SetNPC may never run again. Preserve a valid source
                        // after a temporary native/UI failure and retry slowly.
                        marker.Binding.Failed(now);
                        try { marker.Hide(); }
                        catch { /* Cleanup must not replace the original native failure. */ }
                        WarnAboutTracking(ex);
                    }
                }
                if (TrackingBudgetExpired(started)) budgetExits++;
                if (coopRadarEnabled) RenderCoopDots(localPosition, playerYaw);
            }
            catch (Exception ex) { WarnAboutTracking(ex); }
            finally
            {
                long elapsed = Stopwatch.GetTimestamp() - started;
                trackingFrames++;
                trackingTicks += elapsed;
                if (elapsed > worstTrackingTicks) worstTrackingTicks = elapsed;
            }
        }

        private void ReconcilePlayers(double now, long started)
        {
            if (!reconcilingPlayers)
            {
                if (now < nextPlayerReconcile) return;
                nextPlayerReconcile = now + 0.5;
                playerRegistry = Player.PlayerList;
                if (playerRegistry == null) return;
                playerRegistryIndex = 0;
                playerGeneration++;
                reconcilingPlayers = true;
            }
            // No array copy or full-scene query. Bound even enlarged multiplayer lobbies.
            int processed = 0;
            while (processed < 2 && !TrackingBudgetExpired(started))
            {
                if (playerRegistryIndex >= playerRegistry.Count)
                {
                    staleWorldIds.Clear();
                    foreach (var pair in coopWorldMarkers)
                        if (pair.Value.SeenGeneration != playerGeneration) staleWorldIds.Add(pair.Key);
                    for (int i = 0; i < staleWorldIds.Count; i++) RemoveCoopMarker(staleWorldIds[i]);
                    reconcilingPlayers = false;
                    playerRegistry = null;
                    return;
                }
                Player player = playerRegistry[playerRegistryIndex++];
                processed++;
                if (player == null || player.IsLocalPlayer || player == Player.Local) continue;
                long id = player.Pointer.ToInt64();
                if (!coopWorldMarkers.TryGetValue(id, out WorldMarker marker))
                {
                    marker = new WorldMarker { Id = id, Player = player, Target = player.transform,
                        Category = CoopCategory };
                    coopWorldMarkers.Add(id, marker);
                    coopWork.Upsert(id, marker, CoopCategory, 0.05);
                }
                marker.SeenGeneration = playerGeneration;
            }
        }

        private RectTransform CreateNpcPortrait(WorldMarker marker)
        {
            // Same portrait getter used by NPCPoI.InitializeUI in 0.4.7f12, but no call to
            // InitializeUI/UpdatePosition and no need to activate the phone.
            Sprite portrait = marker.Npc.MugshotSprite;
            if (portrait == null) return null;
            Transform template = marker.Poi.IconContainer;
            if (template == null)
            {
                GameObject prefab = marker.Poi.UIPrefab;
                if (prefab != null) template = prefab.transform.Find("IconContainer");
            }
            if (template == null) return null;
            GameObject view = UnityEngine.Object.Instantiate(template.gameObject);
            view.name = "MinimapNPCPortrait";
            view.transform.SetParent(mapContentObject.transform, false);
            RectTransform rect = view.GetComponent<RectTransform>();
            // Match NPCPoI.InitializeUI exactly. The wrapper also contains
            // Shade/Outline/Fill Images; choosing the first Image changes a
            // decoration instead of the portrait.
            Transform portraitTransform = view.transform.Find("Outline/Icon");
            Image image = portraitTransform != null ? portraitTransform.GetComponent<Image>() : null;
            if (rect == null || image == null)
            {
                UnityEngine.Object.Destroy(view);
                return null;
            }
            image.sprite = portrait;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            if (marker.Category == DealerCategory
                && marker.Npc.TryCast<Il2CppScheduleOne.Economy.Dealer>() != null)
            {
                Transform outline = view.transform.Find("Outline");
                Transform shade = view.transform.Find("Shade");
                Image outlineImage = outline != null ? outline.GetComponent<Image>() : null;
                Image shadeImage = shade != null ? shade.GetComponent<Image>() : null;
                Mask outlineMask = outline != null ? outline.GetComponent<Mask>() : null;
                RectTransform outer = shade != null ? shade.GetComponent<RectTransform>() : null;
                RectTransform clip = outline != null ? outline.GetComponent<RectTransform>() : null;
                if (outlineImage != null && shadeImage != null && outlineMask != null && outer != null && clip != null)
                {
                    // Reuse the existing shadow Image as the outer circle and
                    // the existing Outline Mask as the inset portrait clip.
                    // Shrinking a square portrait alone would leave an uneven ring.
                    shadeImage.sprite = outlineImage.sprite;
                    shadeImage.type = Image.Type.Simple;
                    shadeImage.color = new Color(1f, 179f / 255f, 71f / 255f, 1f);
                    shadeImage.enabled = true;
                    shade.gameObject.SetActive(true);
                    outer.anchorMin = clip.anchorMin;
                    outer.anchorMax = clip.anchorMax;
                    outer.pivot = clip.pivot;
                    outer.sizeDelta = clip.sizeDelta;
                    outer.anchoredPosition = clip.anchoredPosition;
                    outer.localScale = clip.localScale;
                    outer.localRotation = clip.localRotation;
                    clip.SetParent(outer, false);
                    clip.localScale = Vector3.one;
                    clip.localRotation = Quaternion.identity;
                    outlineMask.showMaskGraphic = false;
                    RectTransform portraitRect = portraitTransform.GetComponent<RectTransform>();
                    portraitRect.localScale = Vector3.one;
                    portraitRect.anchorMin = Vector2.zero;
                    portraitRect.anchorMax = Vector2.one;
                    portraitRect.offsetMin = portraitRect.offsetMax = Vector2.zero;
                    marker.DealerBorderOuter = outer;
                    marker.DealerBorderClip = clip;
                    ApplyDealerBorderWidth(marker);
                }
            }
            foreach (Graphic graphic in view.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            return rect;
        }

        private static void ApplyDealerBorderWidth(WorldMarker marker)
        {
            if (marker.DealerBorderOuter == null || marker.DealerBorderClip == null) return;
            // ScreenSpaceOverlay world units are screen pixels. lossyScale
            // includes the map zoom, icon scale and root canvas scale.
            Vector3 scale = marker.DealerBorderOuter.lossyScale;
            Rect bounds = marker.DealerBorderOuter.rect;
            float x = MarkerAppearance.BorderInset(1.5f, Mathf.Abs(scale.x), bounds.width);
            float y = MarkerAppearance.BorderInset(1.5f, Mathf.Abs(scale.y), bounds.height);
            marker.DealerBorderClip.localScale = Vector3.one;
            marker.DealerBorderClip.anchorMin = Vector2.zero;
            marker.DealerBorderClip.anchorMax = Vector2.one;
            marker.DealerBorderClip.offsetMin = new Vector2(x, y);
            marker.DealerBorderClip.offsetMax = new Vector2(-x, -y);
        }

        private void RefreshDealerBorderWidths(bool force = false)
        {
            if (dealerBorderCanvas == null) return;
            float scale = dealerBorderCanvas.scaleFactor;
            if (!force && Mathf.Approximately(scale, lastDealerCanvasScale)) return;
            lastDealerCanvasScale = scale;
            foreach (WorldMarker marker in npcWorldMarkers.Values) ApplyDealerBorderWidth(marker);
        }

        private RectTransform AcquireCoopDot()
        {
            while (unusedCoopDots.Count > 0)
            {
                RectTransform existing = unusedCoopDots.Pop();
                if (existing != null) return existing;
            }
            if (coopDotSprite == null) coopDotSprite = CreateCircleSprite(10, Color.green);
            GameObject view = new GameObject("CoopMarker");
            view.transform.SetParent(cachedMinimapMask, false);
            RectTransform rect = view.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(10f, 10f);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            Image image = view.AddComponent<Image>();
            image.sprite = coopDotSprite;
            image.color = Color.green;
            image.raycastTarget = false;
            return rect;
        }

        private void RenderCoopDots(Vector3 localPosition, float playerYaw)
        {
            float radius = 70f * windowSizeMultiplier - 5f;
            float angle = useNavMode ? playerYaw * Mathf.Deg2Rad : 0;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            foreach (WorldMarker marker in coopWorldMarkers.Values)
            {
                if (!marker.HasPosition || marker.View == null) continue;
                Vector3 delta = marker.Position - localPosition;
                Vector2 offset = new Vector2(delta.x, delta.z) * mapScale * mapZoom;
                if (useNavMode) offset = new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
                bool outside = offset.sqrMagnitude > radius * radius;
                Vector2 mapPosition = outside ? offset.normalized * radius : offset;
                if (marker.View.anchoredPosition != mapPosition) marker.View.anchoredPosition = mapPosition;
                Color color = outside ? new Color(0f, 1f, 0f, 0.4f) : Color.green;
                if (marker.Image.color != color) marker.Image.color = color;
                SetMarkerVisible(marker.View, true);
            }
        }

        private static void SetMarkerVisible(RectTransform view, bool visible)
        {
            if (view != null && view.gameObject.activeSelf != visible) view.gameObject.SetActive(visible);
        }

        private void HideDisabledIndependentMarkers()
        {
            int categories = EnabledWorldCategories;
            foreach (WorldMarker marker in npcWorldMarkers.Values)
                if ((marker.Category & categories) == 0) SetMarkerVisible(marker.View, false);
            if (!coopRadarEnabled)
            {
                foreach (WorldMarker marker in coopWorldMarkers.Values) SetMarkerVisible(marker.View, false);
                reconcilingPlayers = false;
                playerRegistry = null;
            }
        }

        private void RemoveNpcMarker(long id)
        {
            if (!npcWorldMarkers.TryGetValue(id, out WorldMarker marker)) return;
            if (marker.View != null) UnityEngine.Object.Destroy(marker.View.gameObject);
            npcWorldMarkers.Remove(id);
            npcWork.Remove(id);
        }

        private void RemoveCoopMarker(long id)
        {
            if (!coopWorldMarkers.TryGetValue(id, out WorldMarker marker)) return;
            if (marker.View != null)
            {
                SetMarkerVisible(marker.View, false);
                unusedCoopDots.Push(marker.View);
            }
            coopWorldMarkers.Remove(id);
            coopWork.Remove(id);
        }

        private void ResetIndependentMarkers(bool keepSources)
        {
            npcWork.Clear();
            staleWorldIds.Clear();
            foreach (var pair in npcWorldMarkers)
            {
                WorldMarker marker = pair.Value;
                marker.View = null;
                marker.DealerBorderOuter = marker.DealerBorderClip = null;
                if (!keepSources || marker.Poi == null) staleWorldIds.Add(pair.Key);
                else npcWork.Upsert(pair.Key, marker, marker.Category, 0.1);
            }
            for (int i = 0; i < staleWorldIds.Count; i++) npcWorldMarkers.Remove(staleWorldIds[i]);
            coopWorldMarkers.Clear();
            coopWork.Clear();
            unusedCoopDots.Clear();
            playerRegistry = null;
            reconcilingPlayers = false;
            projectionReady = false;
            dealerBorderCanvas = null;
            lastDealerCanvasScale = -1;
            nextProjectionAttempt = nextPlayerReconcile = 0;
        }

        private void TryInitializeWorldProjection()
        {
            MapPositionUtility utility = MapPositionUtility.Instance;
            if (utility == null || utility.MapDimensions <= 0) return;
            float ratio = 500f / utility.MapDimensions;
            Vector2 origin = utility.GetMapPosition(Vector3.zero) * ratio;
            Vector2 x = utility.GetMapPosition(Vector3.right) * ratio - origin;
            Vector2 z = utility.GetMapPosition(Vector3.forward) * ratio - origin;
            if (float.IsNaN(x.x) || float.IsInfinity(x.x) || Mathf.Abs(x.x) < 0.0001f) return;
            worldProjection = new MapProjection(origin.x, origin.y, x.x, x.y, z.x, z.y);
            mapScale = x.x;
            projectionReady = true;
        }

        private void WarnAboutTracking(Exception ex)
        {
            double now = Time.realtimeSinceStartup;
            if (now < nextTrackingWarning) return;
            nextTrackingWarning = now + 10;
            MelonLogger.Warning("Independent marker skipped; map remains active: " + ex.Message);
        }

        private void LogIndependentTrackingStats()
        {
            double milliseconds = 1000.0 / Stopwatch.Frequency;
            MelonLogger.Msg($"Independent markers: NPC sources={npcWorldMarkers.Count}, co-op={coopWorldMarkers.Count}, "
                + $"samples NPC/co-op={npcSamples}/{coopSamples}, average CPU={trackingTicks * milliseconds / Math.Max(1, trackingFrames):F3}ms, "
                + $"worst CPU={worstTrackingTicks * milliseconds:F3}ms, budget stops={budgetExits}. "
                + "CPU includes synchronous native calls; deferred Unity UI rendering is not measured.");
        }

        public override void OnDeinitializeMelon()
        {
            independentMarkerOwner = null;
            if (coopDotSprite != null)
            {
                UnityEngine.Object.Destroy(coopDotSprite.texture);
                UnityEngine.Object.Destroy(coopDotSprite);
            }
        }
    }
}
