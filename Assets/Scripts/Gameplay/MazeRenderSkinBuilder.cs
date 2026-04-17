using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace CIS5680VRGame.Gameplay
{
    public class MazeRenderSkinBuilder : MonoBehaviour
    {
        [SerializeField] Transform m_MazeRoot;
        [SerializeField] Transform m_BoundaryWallsRoot;
        [SerializeField] MeshFilter m_FloorMeshFilter;

        [Header("Materials")]
        [SerializeField] Material m_SourceMaterial;
        [SerializeField] Material m_OutputMaterial;

        [Header("Generated Output")]
        [SerializeField] MeshFilter m_OutputMeshFilter;
        [SerializeField] MeshRenderer m_OutputMeshRenderer;
        [SerializeField] string m_OutputObjectName = "MazeRenderSkin";
        [SerializeField] string m_OutputMeshAssetPath = "Assets/Generated/Maze/Maze1RenderSkin.asset";
        [SerializeField] string m_OutputMaterialAssetPath = "Assets/Generated/Maze/M_Maze1RenderSkinPulse.mat";

        [Header("Geometry")]
        [SerializeField, Min(0.0001f)] float m_SurfaceOffset = 0.003f;
        [SerializeField, Min(0.01f)] float m_UvScale = 1f;
        [SerializeField] bool m_IncludeBoundaryWalls = true;
        [SerializeField] bool m_IncludeWallTopFaces = false;

        public void AutoAssignSceneReferences()
        {
            if (m_MazeRoot == null)
            {
                if (name == "Maze")
                    m_MazeRoot = transform;
                else
                    m_MazeRoot = FindSceneObjectByName("Maze");
            }

            if (m_IncludeBoundaryWalls && m_BoundaryWallsRoot == null)
                m_BoundaryWallsRoot = FindSceneObjectByName("MazeBoundaryWalls");

            if (m_FloorMeshFilter == null)
                m_FloorMeshFilter = FindFloorMeshFilter();

            if (m_SourceMaterial == null)
                m_SourceMaterial = FindDefaultSourceMaterial();

            if ((m_OutputMeshFilter == null || m_OutputMeshRenderer == null) && !string.IsNullOrWhiteSpace(m_OutputObjectName))
                ResolveOutputComponentsFromChild();
        }

        void Reset()
        {
            AutoAssignSceneReferences();
        }

        void OnValidate()
        {
            AutoAssignSceneReferences();
        }

        Transform FindSceneObjectByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName) || !gameObject.scene.IsValid())
                return null;

            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform match = FindRecursiveByName(roots[i].transform, objectName);
                if (match != null)
                    return match;
            }

            return null;
        }

        static Transform FindRecursiveByName(Transform root, string objectName)
        {
            if (root == null)
                return null;

            if (root.name == objectName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindRecursiveByName(root.GetChild(i), objectName);
                if (match != null)
                    return match;
            }

            return null;
        }

        MeshFilter FindFloorMeshFilter()
        {
            Transform searchRoot = m_MazeRoot != null ? m_MazeRoot : transform;
            MeshFilter[] meshFilters = searchRoot.GetComponentsInChildren<MeshFilter>(true);

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter candidate = meshFilters[i];
                if (candidate != null && candidate.name == "Plane")
                    return candidate;
            }

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter candidate = meshFilters[i];
                if (candidate == null || candidate.sharedMesh == null)
                    continue;

                Renderer candidateRenderer = candidate.GetComponent<Renderer>();
                if (candidateRenderer == null)
                    continue;

                Vector3 size = candidateRenderer.bounds.size;
                float horizontal = Mathf.Max(size.x, size.z);
                if (size.y <= horizontal * 0.05f)
                    return candidate;
            }

            return null;
        }

        Material FindDefaultSourceMaterial()
        {
            if (TryFindWallMaterial(m_MazeRoot, out Material wallMaterial))
                return wallMaterial;

            if (m_IncludeBoundaryWalls && TryFindWallMaterial(m_BoundaryWallsRoot, out wallMaterial))
                return wallMaterial;

            Renderer floorRenderer = m_FloorMeshFilter != null ? m_FloorMeshFilter.GetComponent<Renderer>() : null;
            return floorRenderer != null ? floorRenderer.sharedMaterial : null;
        }

        static bool TryFindWallMaterial(Transform root, out Material material)
        {
            material = null;
            if (root == null)
                return false;

            BoxCollider[] colliders = root.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider collider = colliders[i];
                if (collider == null || !collider.enabled)
                    continue;

                Renderer renderer = collider.GetComponent<Renderer>();
                if (renderer == null || renderer.sharedMaterial == null)
                    continue;

                material = renderer.sharedMaterial;
                return true;
            }

            return false;
        }

        void ResolveOutputComponentsFromChild()
        {
            Transform child = transform.Find(m_OutputObjectName);
            if (child == null)
                return;

            if (m_OutputMeshFilter == null)
                m_OutputMeshFilter = child.GetComponent<MeshFilter>();

            if (m_OutputMeshRenderer == null)
                m_OutputMeshRenderer = child.GetComponent<MeshRenderer>();
        }

#if UNITY_EDITOR
        const string PulseRevealShaderName = "SonarBounce/PulseReveal";
        static readonly int s_UseMeshGridUvId = Shader.PropertyToID("_UseMeshGridUv");

        struct WallPatch
        {
            public BoxCollider SourceCollider;
            public Vector3 Center;
            public Vector3 HalfWidth;
            public Vector3 HalfHeight;
            public Vector3 HalfThickness;
        }

        struct OrientedBox
        {
            public Vector3 Center;
            public Vector3 AxisX;
            public Vector3 AxisY;
            public Vector3 AxisZ;
            public float ExtentX;
            public float ExtentY;
            public float ExtentZ;
        }

        struct WallFacePatch
        {
            public BoxCollider SourceCollider;
            public Vector3 PlaneCenter;
            public Vector3 EmitCenter;
            public Vector3 Normal;
            public Vector3 Tangent;
            public Vector3 Bitangent;
            public float TangentExtent;
            public float BitangentExtent;
        }

        struct SurfaceRect
        {
            public Vector2 Min;
            public Vector2 Max;

            public float Width => Max.x - Min.x;
            public float Height => Max.y - Min.y;
        }

        sealed class FaceRecord
        {
            public readonly Vector3[] BaseCorners = new Vector3[4];
            public readonly Vector3[] EmitCorners = new Vector3[4];
            public readonly Vector2[] Uvs = new Vector2[4];
            public Vector3 Normal;
            public bool UvAssigned;
        }

        struct EdgeReference
        {
            public int FaceIndex;
            public int StartIndex;
            public int EndIndex;
        }

        internal void BuildRenderSkin()
        {
            AutoAssignSceneReferences();

            if (m_MazeRoot == null)
                throw new UnityException("Maze root is missing.");

            if (m_FloorMeshFilter == null || m_FloorMeshFilter.sharedMesh == null)
                throw new UnityException("Floor MeshFilter is missing.");

            Mesh renderMesh = GetOrCreateMeshAsset();
            Material renderMaterial = GetOrCreateOutputMaterial();
            EnsureOutputComponents(renderMesh, renderMaterial);

            List<FaceRecord> faceRecords = new();
            AppendFloorPatch(faceRecords);

            List<BoxCollider> wallColliders = CollectWallColliders();
            AppendWallPatches(wallColliders, faceRecords);
            AssignContinuousUvs(faceRecords);

            List<Vector3> vertices = new();
            List<Vector3> normals = new();
            List<Vector2> uvs = new();
            List<int> triangles = new();

            for (int i = 0; i < faceRecords.Count; i++)
                AppendFaceRecord(faceRecords[i], vertices, normals, uvs, triangles);

            renderMesh.Clear();
            renderMesh.name = Path.GetFileNameWithoutExtension(m_OutputMeshAssetPath);
            renderMesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            renderMesh.SetVertices(vertices);
            renderMesh.SetNormals(normals);
            renderMesh.SetUVs(0, uvs);
            renderMesh.SetTriangles(triangles, 0, true);
            renderMesh.RecalculateBounds();

            EditorUtility.SetDirty(renderMesh);
            EditorUtility.SetDirty(m_OutputMeshFilter);
            EditorUtility.SetDirty(m_OutputMeshRenderer);
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        internal void SelectOrCreateBuilderOutput()
        {
            AutoAssignSceneReferences();
            EnsureOutputComponents(null, null);
            if (m_OutputMeshFilter != null)
                Selection.activeObject = m_OutputMeshFilter.gameObject;
        }

        Mesh GetOrCreateMeshAsset()
        {
            string assetPath = NormalizeAssetPath(m_OutputMeshAssetPath, "Assets/Generated/Maze/Maze1RenderSkin.asset", ".asset");
            EnsureAssetFolders(assetPath);
            m_OutputMeshAssetPath = assetPath;

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh != null)
                return mesh;

            mesh = new Mesh
            {
                name = Path.GetFileNameWithoutExtension(assetPath)
            };

            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        Material GetOrCreateOutputMaterial()
        {
            if (m_OutputMaterial != null)
            {
                ForceUvGridMode(m_OutputMaterial);
                EditorUtility.SetDirty(m_OutputMaterial);
                return m_OutputMaterial;
            }

            Material sourceMaterial = m_SourceMaterial != null ? m_SourceMaterial : FindDefaultSourceMaterial();
            if (sourceMaterial == null)
                throw new UnityException("Source material is missing. Assign one in the builder inspector.");

            string assetPath = NormalizeAssetPath(m_OutputMaterialAssetPath, "Assets/Generated/Maze/M_Maze1RenderSkinPulse.mat", ".mat");
            EnsureAssetFolders(assetPath);
            m_OutputMaterialAssetPath = assetPath;

            Material outputMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (outputMaterial == null)
            {
                outputMaterial = new Material(sourceMaterial)
                {
                    name = Path.GetFileNameWithoutExtension(assetPath)
                };
                AssetDatabase.CreateAsset(outputMaterial, assetPath);
            }

            ForceUvGridMode(outputMaterial);
            EditorUtility.SetDirty(outputMaterial);
            m_OutputMaterial = outputMaterial;
            return outputMaterial;
        }

        void EnsureOutputComponents(Mesh mesh, Material material)
        {
            GameObject outputObject = m_OutputMeshFilter != null ? m_OutputMeshFilter.gameObject : null;
            if (outputObject == null)
            {
                Transform existingChild = transform.Find(m_OutputObjectName);
                outputObject = existingChild != null ? existingChild.gameObject : null;
            }

            if (outputObject == null)
            {
                outputObject = new GameObject(m_OutputObjectName);
                Undo.RegisterCreatedObjectUndo(outputObject, "Create Maze Render Skin");
                outputObject.transform.SetParent(transform, false);
            }

            outputObject.layer = gameObject.layer;

            m_OutputMeshFilter = outputObject.GetComponent<MeshFilter>();
            if (m_OutputMeshFilter == null)
                m_OutputMeshFilter = Undo.AddComponent<MeshFilter>(outputObject);

            m_OutputMeshRenderer = outputObject.GetComponent<MeshRenderer>();
            if (m_OutputMeshRenderer == null)
                m_OutputMeshRenderer = Undo.AddComponent<MeshRenderer>(outputObject);

            if (mesh != null)
                m_OutputMeshFilter.sharedMesh = mesh;

            if (material != null)
                m_OutputMeshRenderer.sharedMaterial = material;

            m_OutputMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_OutputMeshRenderer.receiveShadows = false;
            m_OutputMeshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            m_OutputMeshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        void AppendFloorPatch(List<FaceRecord> faceRecords)
        {
            Mesh floorMesh = m_FloorMeshFilter.sharedMesh;
            Transform floorTransform = m_FloorMeshFilter.transform;
            Bounds localBounds = floorMesh.bounds;

            Vector3 center = floorTransform.TransformPoint(localBounds.center);
            Vector3 halfRight = floorTransform.TransformVector(Vector3.right * localBounds.extents.x);
            Vector3 halfForward = floorTransform.TransformVector(Vector3.forward * localBounds.extents.z);
            Vector3 normal = floorTransform.TransformDirection(Vector3.up).normalized;
            Vector3 offset = normal * m_SurfaceOffset;

            AddFaceRecord(
                center - halfRight - halfForward,
                center + halfRight - halfForward,
                center + halfRight + halfForward,
                center - halfRight + halfForward,
                center - halfRight - halfForward + offset,
                center + halfRight - halfForward + offset,
                center + halfRight + halfForward + offset,
                center - halfRight + halfForward + offset,
                normal,
                faceRecords);
        }

        List<BoxCollider> CollectWallColliders()
        {
            List<BoxCollider> colliders = new();
            CollectWallCollidersFromRoot(m_MazeRoot, colliders);

            if (m_IncludeBoundaryWalls)
                CollectWallCollidersFromRoot(m_BoundaryWallsRoot, colliders);

            return colliders;
        }

        void CollectWallCollidersFromRoot(Transform root, List<BoxCollider> colliders)
        {
            if (root == null || colliders == null)
                return;

            BoxCollider[] childColliders = root.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                BoxCollider collider = childColliders[i];
                if (collider == null || !collider.enabled)
                    continue;

                if (m_FloorMeshFilter != null && collider.transform == m_FloorMeshFilter.transform)
                    continue;

                colliders.Add(collider);
            }
        }

        void AppendWallPatches(List<BoxCollider> wallColliders, List<FaceRecord> faceRecords)
        {
            if (wallColliders == null || wallColliders.Count == 0)
                return;

            for (int i = 0; i < wallColliders.Count; i++)
            {
                BoxCollider wallCollider = wallColliders[i];
                if (!TryGetWallPatch(wallCollider, out WallPatch patch))
                    continue;

                AppendWallSideFaces(patch, wallColliders, faceRecords);

                if (m_IncludeWallTopFaces)
                    AppendWallTopFace(patch, wallColliders, faceRecords);
            }
        }

        bool TryGetWallPatch(BoxCollider collider, out WallPatch patch)
        {
            patch = default;
            if (collider == null || !collider.enabled)
                return false;

            Transform wallTransform = collider.transform;
            Vector3[] halfAxes =
            {
                wallTransform.TransformVector(Vector3.right * (collider.size.x * 0.5f)),
                wallTransform.TransformVector(Vector3.up * (collider.size.y * 0.5f)),
                wallTransform.TransformVector(Vector3.forward * (collider.size.z * 0.5f))
            };

            float[] magnitudes =
            {
                halfAxes[0].magnitude,
                halfAxes[1].magnitude,
                halfAxes[2].magnitude
            };

            int thinAxis = 0;
            if (magnitudes[1] < magnitudes[thinAxis])
                thinAxis = 1;
            if (magnitudes[2] < magnitudes[thinAxis])
                thinAxis = 2;

            int heightAxis = 0;
            float bestUpAlignment = Mathf.Abs(Vector3.Dot(halfAxes[0].normalized, Vector3.up));
            for (int i = 1; i < halfAxes.Length; i++)
            {
                float upAlignment = Mathf.Abs(Vector3.Dot(halfAxes[i].normalized, Vector3.up));
                if (upAlignment > bestUpAlignment)
                {
                    bestUpAlignment = upAlignment;
                    heightAxis = i;
                }
            }

            if (thinAxis == heightAxis)
                return false;

            int widthAxis = 3 - thinAxis - heightAxis;

            if (magnitudes[heightAxis] <= 0.01f || magnitudes[widthAxis] <= 0.01f || magnitudes[thinAxis] <= 0.001f)
                return false;

            patch.SourceCollider = collider;
            patch.Center = wallTransform.TransformPoint(collider.center);
            patch.HalfWidth = halfAxes[widthAxis];
            patch.HalfHeight = halfAxes[heightAxis];
            patch.HalfThickness = halfAxes[thinAxis];

            if (Vector3.Dot(patch.HalfHeight, Vector3.up) < 0f)
                patch.HalfHeight = -patch.HalfHeight;

            return true;
        }

        void AppendWallSideFaces(WallPatch patch, List<BoxCollider> wallColliders, List<FaceRecord> faceRecords)
        {
            Vector3[] thicknessDirections = { patch.HalfThickness.normalized, -patch.HalfThickness.normalized };
            Vector3[] faceCenters =
            {
                patch.Center + patch.HalfThickness,
                patch.Center - patch.HalfThickness
            };

            for (int i = 0; i < faceCenters.Length; i++)
            {
                WallFacePatch facePatch = CreateWallFacePatch(patch.SourceCollider, faceCenters[i], thicknessDirections[i], patch.HalfWidth, patch.HalfHeight);
                AppendTrimmedWallFace(facePatch, wallColliders, faceRecords);
            }
        }

        void AppendWallTopFace(WallPatch patch, List<BoxCollider> wallColliders, List<FaceRecord> faceRecords)
        {
            Vector3 normal = patch.HalfHeight.normalized;
            Vector3 topCenter = patch.Center + patch.HalfHeight;
            WallFacePatch topFacePatch = CreateWallFacePatch(patch.SourceCollider, topCenter, normal, patch.HalfWidth, patch.HalfThickness);
            AppendTrimmedWallFace(topFacePatch, wallColliders, faceRecords);
        }

        WallFacePatch CreateWallFacePatch(BoxCollider sourceCollider, Vector3 planeCenter, Vector3 normal, Vector3 tangentExtentVector, Vector3 bitangentExtentVector)
        {
            normal = normal.normalized;

            return new WallFacePatch
            {
                SourceCollider = sourceCollider,
                PlaneCenter = planeCenter,
                EmitCenter = planeCenter + normal * m_SurfaceOffset,
                Normal = normal,
                Tangent = tangentExtentVector.normalized,
                Bitangent = bitangentExtentVector.normalized,
                TangentExtent = tangentExtentVector.magnitude,
                BitangentExtent = bitangentExtentVector.magnitude
            };
        }

        void AppendTrimmedWallFace(WallFacePatch facePatch, List<BoxCollider> wallColliders, List<FaceRecord> faceRecords)
        {
            List<SurfaceRect> remainingRects = new()
            {
                new SurfaceRect
                {
                    Min = new Vector2(-facePatch.TangentExtent, -facePatch.BitangentExtent),
                    Max = new Vector2(facePatch.TangentExtent, facePatch.BitangentExtent)
                }
            };

            for (int i = 0; i < wallColliders.Count; i++)
            {
                BoxCollider otherCollider = wallColliders[i];
                if (otherCollider == null || otherCollider == facePatch.SourceCollider)
                    continue;

                if (!TryBuildOccluderRect(facePatch, otherCollider, out SurfaceRect occluder))
                    continue;

                SubtractOccluder(remainingRects, occluder);
                if (remainingRects.Count == 0)
                    return;
            }

            for (int i = 0; i < remainingRects.Count; i++)
            {
                SurfaceRect rect = remainingRects[i];
                if (rect.Width <= 0.001f || rect.Height <= 0.001f)
                    continue;

                Vector3 baseBottomLeft = facePatch.PlaneCenter + facePatch.Tangent * rect.Min.x + facePatch.Bitangent * rect.Min.y;
                Vector3 baseBottomRight = facePatch.PlaneCenter + facePatch.Tangent * rect.Max.x + facePatch.Bitangent * rect.Min.y;
                Vector3 baseTopRight = facePatch.PlaneCenter + facePatch.Tangent * rect.Max.x + facePatch.Bitangent * rect.Max.y;
                Vector3 baseTopLeft = facePatch.PlaneCenter + facePatch.Tangent * rect.Min.x + facePatch.Bitangent * rect.Max.y;

                Vector3 emitBottomLeft = facePatch.EmitCenter + facePatch.Tangent * rect.Min.x + facePatch.Bitangent * rect.Min.y;
                Vector3 emitBottomRight = facePatch.EmitCenter + facePatch.Tangent * rect.Max.x + facePatch.Bitangent * rect.Min.y;
                Vector3 emitTopRight = facePatch.EmitCenter + facePatch.Tangent * rect.Max.x + facePatch.Bitangent * rect.Max.y;
                Vector3 emitTopLeft = facePatch.EmitCenter + facePatch.Tangent * rect.Min.x + facePatch.Bitangent * rect.Max.y;

                AddFaceRecord(
                    baseBottomLeft,
                    baseBottomRight,
                    baseTopRight,
                    baseTopLeft,
                    emitBottomLeft,
                    emitBottomRight,
                    emitTopRight,
                    emitTopLeft,
                    facePatch.Normal,
                    faceRecords);
            }
        }

        bool TryBuildOccluderRect(WallFacePatch facePatch, BoxCollider otherCollider, out SurfaceRect occluderRect)
        {
            occluderRect = default;

            OrientedBox otherBox = GetOrientedBox(otherCollider);
            Vector3 toOther = otherBox.Center - facePatch.PlaneCenter;

            float normalCenter = Vector3.Dot(toOther, facePatch.Normal);
            float normalExtent = ProjectExtent(otherBox, facePatch.Normal);
            if (normalCenter + normalExtent <= -0.001f)
                return false;

            float tangentCenter = Vector3.Dot(toOther, facePatch.Tangent);
            float tangentExtent = ProjectExtent(otherBox, facePatch.Tangent);
            float bitangentCenter = Vector3.Dot(toOther, facePatch.Bitangent);
            float bitangentExtent = ProjectExtent(otherBox, facePatch.Bitangent);

            SurfaceRect candidateRect = new()
            {
                Min = new Vector2(tangentCenter - tangentExtent, bitangentCenter - bitangentExtent),
                Max = new Vector2(tangentCenter + tangentExtent, bitangentCenter + bitangentExtent)
            };

            SurfaceRect faceBounds = new()
            {
                Min = new Vector2(-facePatch.TangentExtent, -facePatch.BitangentExtent),
                Max = new Vector2(facePatch.TangentExtent, facePatch.BitangentExtent)
            };

            if (!TryIntersectRect(faceBounds, candidateRect, out occluderRect))
                return false;

            return occluderRect.Width > 0.001f && occluderRect.Height > 0.001f;
        }

        static OrientedBox GetOrientedBox(BoxCollider collider)
        {
            Transform boxTransform = collider.transform;
            Vector3 halfAxisX = boxTransform.TransformVector(Vector3.right * (collider.size.x * 0.5f));
            Vector3 halfAxisY = boxTransform.TransformVector(Vector3.up * (collider.size.y * 0.5f));
            Vector3 halfAxisZ = boxTransform.TransformVector(Vector3.forward * (collider.size.z * 0.5f));

            return new OrientedBox
            {
                Center = boxTransform.TransformPoint(collider.center),
                AxisX = halfAxisX.normalized,
                AxisY = halfAxisY.normalized,
                AxisZ = halfAxisZ.normalized,
                ExtentX = halfAxisX.magnitude,
                ExtentY = halfAxisY.magnitude,
                ExtentZ = halfAxisZ.magnitude
            };
        }

        static float ProjectExtent(OrientedBox box, Vector3 axis)
        {
            return Mathf.Abs(Vector3.Dot(axis, box.AxisX)) * box.ExtentX
                + Mathf.Abs(Vector3.Dot(axis, box.AxisY)) * box.ExtentY
                + Mathf.Abs(Vector3.Dot(axis, box.AxisZ)) * box.ExtentZ;
        }

        static bool TryIntersectRect(SurfaceRect a, SurfaceRect b, out SurfaceRect intersection)
        {
            intersection = new SurfaceRect
            {
                Min = new Vector2(Mathf.Max(a.Min.x, b.Min.x), Mathf.Max(a.Min.y, b.Min.y)),
                Max = new Vector2(Mathf.Min(a.Max.x, b.Max.x), Mathf.Min(a.Max.y, b.Max.y))
            };

            return intersection.Width > 0.001f && intersection.Height > 0.001f;
        }

        static void SubtractOccluder(List<SurfaceRect> sourceRects, SurfaceRect occluder)
        {
            if (sourceRects == null || sourceRects.Count == 0)
                return;

            List<SurfaceRect> nextRects = new();

            for (int i = 0; i < sourceRects.Count; i++)
            {
                SurfaceRect source = sourceRects[i];
                if (!TryIntersectRect(source, occluder, out SurfaceRect intersection))
                {
                    nextRects.Add(source);
                    continue;
                }

                if (intersection.Min.x > source.Min.x + 0.001f)
                {
                    nextRects.Add(new SurfaceRect
                    {
                        Min = source.Min,
                        Max = new Vector2(intersection.Min.x, source.Max.y)
                    });
                }

                if (intersection.Max.x < source.Max.x - 0.001f)
                {
                    nextRects.Add(new SurfaceRect
                    {
                        Min = new Vector2(intersection.Max.x, source.Min.y),
                        Max = source.Max
                    });
                }

                float clippedMinX = Mathf.Max(source.Min.x, intersection.Min.x);
                float clippedMaxX = Mathf.Min(source.Max.x, intersection.Max.x);

                if (intersection.Min.y > source.Min.y + 0.001f)
                {
                    nextRects.Add(new SurfaceRect
                    {
                        Min = new Vector2(clippedMinX, source.Min.y),
                        Max = new Vector2(clippedMaxX, intersection.Min.y)
                    });
                }

                if (intersection.Max.y < source.Max.y - 0.001f)
                {
                    nextRects.Add(new SurfaceRect
                    {
                        Min = new Vector2(clippedMinX, intersection.Max.y),
                        Max = new Vector2(clippedMaxX, source.Max.y)
                    });
                }
            }

            sourceRects.Clear();
            for (int i = 0; i < nextRects.Count; i++)
            {
                SurfaceRect rect = nextRects[i];
                if (rect.Width <= 0.001f || rect.Height <= 0.001f)
                    continue;

                sourceRects.Add(rect);
            }
        }

        void AddFaceRecord(
            Vector3 baseBottomLeft,
            Vector3 baseBottomRight,
            Vector3 baseTopRight,
            Vector3 baseTopLeft,
            Vector3 emitBottomLeft,
            Vector3 emitBottomRight,
            Vector3 emitTopRight,
            Vector3 emitTopLeft,
            Vector3 worldNormal,
            List<FaceRecord> faceRecords)
        {
            Vector3 baseCross = Vector3.Cross(baseBottomRight - baseBottomLeft, baseTopLeft - baseBottomLeft);
            if (Vector3.Dot(baseCross, worldNormal) < 0f)
            {
                Swap(ref baseBottomRight, ref baseTopLeft);
                Swap(ref emitBottomRight, ref emitTopLeft);
            }

            FaceRecord faceRecord = new()
            {
                Normal = worldNormal.normalized
            };

            faceRecord.BaseCorners[0] = baseBottomLeft;
            faceRecord.BaseCorners[1] = baseBottomRight;
            faceRecord.BaseCorners[2] = baseTopRight;
            faceRecord.BaseCorners[3] = baseTopLeft;

            faceRecord.EmitCorners[0] = emitBottomLeft;
            faceRecord.EmitCorners[1] = emitBottomRight;
            faceRecord.EmitCorners[2] = emitTopRight;
            faceRecord.EmitCorners[3] = emitTopLeft;

            faceRecords.Add(faceRecord);
        }

        void AssignContinuousUvs(List<FaceRecord> faceRecords)
        {
            if (faceRecords == null || faceRecords.Count == 0)
                return;

            Dictionary<string, List<EdgeReference>> edgeMap = BuildEdgeMap(faceRecords);
            Queue<int> pendingFaces = new();

            for (int i = 0; i < faceRecords.Count; i++)
            {
                if (!IsFloorLikeFace(faceRecords[i].Normal))
                    continue;

                SeedFaceUvs(faceRecords[i]);
                pendingFaces.Enqueue(i);
            }

            if (pendingFaces.Count == 0 && faceRecords.Count > 0)
            {
                SeedFaceUvs(faceRecords[0]);
                pendingFaces.Enqueue(0);
            }

            while (pendingFaces.Count > 0)
            {
                int currentFaceIndex = pendingFaces.Dequeue();
                FaceRecord currentFace = faceRecords[currentFaceIndex];

                for (int edgeStart = 0; edgeStart < 4; edgeStart++)
                {
                    int edgeEnd = (edgeStart + 1) % 4;
                    string edgeKey = BuildEdgeKey(currentFace.BaseCorners[edgeStart], currentFace.BaseCorners[edgeEnd]);
                    if (!edgeMap.TryGetValue(edgeKey, out List<EdgeReference> sharedEdges))
                        continue;

                    for (int i = 0; i < sharedEdges.Count; i++)
                    {
                        EdgeReference sharedEdge = sharedEdges[i];
                        if (sharedEdge.FaceIndex == currentFaceIndex)
                            continue;

                        FaceRecord neighborFace = faceRecords[sharedEdge.FaceIndex];
                        if (neighborFace.UvAssigned)
                            continue;

                        AssignNeighborUvs(currentFace, edgeStart, edgeEnd, neighborFace, sharedEdge.StartIndex, sharedEdge.EndIndex);
                        pendingFaces.Enqueue(sharedEdge.FaceIndex);
                    }
                }
            }

            for (int i = 0; i < faceRecords.Count; i++)
            {
                FaceRecord faceRecord = faceRecords[i];
                if (faceRecord.UvAssigned)
                    continue;

                SeedFaceUvs(faceRecord);
            }
        }

        Dictionary<string, List<EdgeReference>> BuildEdgeMap(List<FaceRecord> faceRecords)
        {
            Dictionary<string, List<EdgeReference>> edgeMap = new();

            for (int faceIndex = 0; faceIndex < faceRecords.Count; faceIndex++)
            {
                FaceRecord faceRecord = faceRecords[faceIndex];
                for (int startIndex = 0; startIndex < 4; startIndex++)
                {
                    int endIndex = (startIndex + 1) % 4;
                    string edgeKey = BuildEdgeKey(faceRecord.BaseCorners[startIndex], faceRecord.BaseCorners[endIndex]);

                    if (!edgeMap.TryGetValue(edgeKey, out List<EdgeReference> edges))
                    {
                        edges = new List<EdgeReference>(2);
                        edgeMap.Add(edgeKey, edges);
                    }

                    edges.Add(new EdgeReference
                    {
                        FaceIndex = faceIndex,
                        StartIndex = startIndex,
                        EndIndex = endIndex
                    });
                }
            }

            return edgeMap;
        }

        void SeedFaceUvs(FaceRecord faceRecord)
        {
            for (int i = 0; i < 4; i++)
                faceRecord.Uvs[i] = ComputeContinuousGridUv(faceRecord.BaseCorners[i], faceRecord.Normal);

            faceRecord.UvAssigned = true;
        }

        static bool IsFloorLikeFace(Vector3 normal)
        {
            return Mathf.Abs(Vector3.Dot(normal.normalized, Vector3.up)) >= 0.9f;
        }

        void AssignNeighborUvs(FaceRecord currentFace, int currentStartIndex, int currentEndIndex, FaceRecord neighborFace, int neighborStartIndex, int neighborEndIndex)
        {
            int mappedNeighborStart = neighborStartIndex;
            int mappedNeighborEnd = neighborEndIndex;

            float startMatchDistance = (neighborFace.BaseCorners[neighborStartIndex] - currentFace.BaseCorners[currentStartIndex]).sqrMagnitude;
            float flippedStartMatchDistance = (neighborFace.BaseCorners[neighborEndIndex] - currentFace.BaseCorners[currentStartIndex]).sqrMagnitude;
            if (flippedStartMatchDistance < startMatchDistance)
            {
                mappedNeighborStart = neighborEndIndex;
                mappedNeighborEnd = neighborStartIndex;
            }

            Vector2 uvStart = currentFace.Uvs[currentStartIndex];
            Vector2 uvEnd = currentFace.Uvs[currentEndIndex];
            neighborFace.Uvs[mappedNeighborStart] = uvStart;
            neighborFace.Uvs[mappedNeighborEnd] = uvEnd;

            Vector2 edgeDelta = uvEnd - uvStart;
            float edgeLength = edgeDelta.magnitude;
            if (edgeLength <= 0.0001f)
            {
                SeedFaceUvs(neighborFace);
                return;
            }

            Vector2 edgeDirection = edgeDelta / edgeLength;
            Vector2 edgeNormal = new Vector2(-edgeDirection.y, edgeDirection.x);

            float currentSideSign = ComputeFaceSideSign(currentFace, currentStartIndex, currentEndIndex, uvStart, edgeDirection);
            if (Mathf.Abs(currentSideSign) < 0.5f)
                currentSideSign = 1f;

            edgeNormal *= -currentSideSign;

            float neighborDepth = ComputeNeighborDepth(neighborFace, mappedNeighborStart, mappedNeighborEnd) * m_UvScale;
            Vector2 perpendicularOffset = edgeNormal * neighborDepth;

            int[] oppositeIndices = GetOppositeEdgeVertexIndices(mappedNeighborStart, mappedNeighborEnd);
            int firstOpposite = oppositeIndices[0];
            int secondOpposite = oppositeIndices[1];

            Vector3 neighborStart = neighborFace.BaseCorners[mappedNeighborStart];
            Vector3 neighborEnd = neighborFace.BaseCorners[mappedNeighborEnd];
            float firstToStart = (neighborFace.BaseCorners[firstOpposite] - neighborStart).sqrMagnitude;
            float firstToEnd = (neighborFace.BaseCorners[firstOpposite] - neighborEnd).sqrMagnitude;
            if (firstToEnd < firstToStart)
            {
                firstOpposite = oppositeIndices[1];
                secondOpposite = oppositeIndices[0];
            }

            neighborFace.Uvs[firstOpposite] = uvStart + perpendicularOffset;
            neighborFace.Uvs[secondOpposite] = uvEnd + perpendicularOffset;
            neighborFace.UvAssigned = true;
        }

        static float ComputeFaceSideSign(FaceRecord faceRecord, int edgeStartIndex, int edgeEndIndex, Vector2 edgeOrigin, Vector2 edgeDirection)
        {
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < 4; i++)
                centroid += faceRecord.Uvs[i];
            centroid *= 0.25f;

            return Mathf.Sign(Cross(edgeDirection, centroid - edgeOrigin));
        }

        static float ComputeNeighborDepth(FaceRecord faceRecord, int edgeStartIndex, int edgeEndIndex)
        {
            int[] oppositeIndices = GetOppositeEdgeVertexIndices(edgeStartIndex, edgeEndIndex);
            Vector3 sharedMidpoint = (faceRecord.BaseCorners[edgeStartIndex] + faceRecord.BaseCorners[edgeEndIndex]) * 0.5f;
            Vector3 oppositeMidpoint = (faceRecord.BaseCorners[oppositeIndices[0]] + faceRecord.BaseCorners[oppositeIndices[1]]) * 0.5f;
            return Vector3.Distance(sharedMidpoint, oppositeMidpoint);
        }

        static int[] GetOppositeEdgeVertexIndices(int edgeStartIndex, int edgeEndIndex)
        {
            int[] remaining = new int[2];
            int writeIndex = 0;

            for (int i = 0; i < 4; i++)
            {
                if (i == edgeStartIndex || i == edgeEndIndex)
                    continue;

                remaining[writeIndex++] = i;
            }

            return remaining;
        }

        void AppendFaceRecord(FaceRecord faceRecord, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            int startIndex = vertices.Count;

            for (int i = 0; i < 4; i++)
            {
                vertices.Add(transform.InverseTransformPoint(faceRecord.EmitCorners[i]));
                normals.Add(transform.InverseTransformDirection(faceRecord.Normal).normalized);
                uvs.Add(faceRecord.Uvs[i]);
            }

            triangles.Add(startIndex + 0);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 0);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }

        Vector2 ComputeContinuousGridUv(Vector3 worldPoint, Vector3 worldNormal)
        {
            Vector3 absNormal = Abs(worldNormal.normalized);
            Vector2 uv;

            if (absNormal.y >= absNormal.x && absNormal.y >= absNormal.z)
            {
                uv = new Vector2(worldPoint.x, worldPoint.z);
            }
            else if (absNormal.x >= absNormal.z)
            {
                uv = new Vector2(worldPoint.x + worldPoint.y, worldPoint.z);
            }
            else
            {
                uv = new Vector2(worldPoint.x, worldPoint.z + worldPoint.y);
            }

            return uv * m_UvScale;
        }

        static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        string BuildEdgeKey(Vector3 pointA, Vector3 pointB)
        {
            Vector3 roundedA = RoundEdgePoint(pointA);
            Vector3 roundedB = RoundEdgePoint(pointB);

            if (CompareRoundedPoints(roundedA, roundedB) > 0)
                Swap(ref roundedA, ref roundedB);

            return $"{roundedA.x:F4},{roundedA.y:F4},{roundedA.z:F4}|{roundedB.x:F4},{roundedB.y:F4},{roundedB.z:F4}";
        }

        static Vector3 RoundEdgePoint(Vector3 point)
        {
            const float precision = 1000f;
            return new Vector3(
                Mathf.Round(point.x * precision) / precision,
                Mathf.Round(point.y * precision) / precision,
                Mathf.Round(point.z * precision) / precision);
        }

        static int CompareRoundedPoints(Vector3 a, Vector3 b)
        {
            int xCompare = a.x.CompareTo(b.x);
            if (xCompare != 0)
                return xCompare;

            int yCompare = a.y.CompareTo(b.y);
            if (yCompare != 0)
                return yCompare;

            return a.z.CompareTo(b.z);
        }

        static float Cross(Vector2 a, Vector2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }

        static void Swap<T>(ref T first, ref T second)
        {
            T temp = first;
            first = second;
            second = temp;
        }

        static void ForceUvGridMode(Material material)
        {
            if (material == null)
                return;

            Shader pulseRevealShader = Shader.Find(PulseRevealShaderName);
            if (pulseRevealShader != null && material.shader != pulseRevealShader)
                material.shader = pulseRevealShader;

            if (material.HasProperty(s_UseMeshGridUvId))
                material.SetFloat(s_UseMeshGridUvId, 1f);
        }

        static string NormalizeAssetPath(string assetPath, string fallbackAssetPath, string requiredExtension)
        {
            string normalizedPath = string.IsNullOrWhiteSpace(assetPath) ? fallbackAssetPath : assetPath.Trim().Replace('\\', '/');
            if (!normalizedPath.StartsWith("Assets/"))
                normalizedPath = fallbackAssetPath;

            if (!normalizedPath.EndsWith(requiredExtension))
                normalizedPath = $"{normalizedPath}{requiredExtension}";

            return normalizedPath;
        }

        static void EnsureAssetFolders(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory) || directory == "Assets")
                return;

            string[] segments = directory.Split('/');
            string currentPath = segments[0];

            for (int i = 1; i < segments.Length; i++)
            {
                string nextPath = $"{currentPath}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                    AssetDatabase.CreateFolder(currentPath, segments[i]);

                currentPath = nextPath;
            }
        }
    }

    [CustomEditor(typeof(MazeRenderSkinBuilder))]
    sealed class MazeRenderSkinBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Builds a non-destructive render-only skin that overlays the floor and wall faces with continuous UVs. Colliders and gameplay objects stay unchanged.",
                MessageType.Info);

            MazeRenderSkinBuilder builder = (MazeRenderSkinBuilder)target;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto Fill References"))
            {
                Undo.RecordObject(builder, "Auto Fill Maze Render Skin Builder");
                builder.AutoAssignSceneReferences();
                EditorUtility.SetDirty(builder);
            }

            if (GUILayout.Button("Select Output"))
                builder.SelectOrCreateBuilderOutput();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Build / Rebuild Render Skin"))
            {
                try
                {
                    Undo.RecordObject(builder, "Build Maze Render Skin");
                    builder.BuildRenderSkin();
                }
                catch (UnityException ex)
                {
                    Debug.LogError(ex.Message, builder);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("GameObject/CIS5680/Add Maze Render Skin Builder", false, 10)]
        static void AddBuilderToSelection()
        {
            GameObject target = Selection.activeGameObject;
            if (target == null)
            {
                Debug.LogWarning("Select the Maze root before adding MazeRenderSkinBuilder.");
                return;
            }

            MazeRenderSkinBuilder builder = target.GetComponent<MazeRenderSkinBuilder>();
            if (builder == null)
                builder = Undo.AddComponent<MazeRenderSkinBuilder>(target);

            builder.AutoAssignSceneReferences();
            EditorUtility.SetDirty(builder);
            Selection.activeObject = builder;
        }
    }
#endif
}
