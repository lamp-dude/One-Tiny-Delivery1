using System.Collections;
using System.Collections.Generic;

using System.Linq;

using UnityEngine;



namespace MicahW.PointGrass {
    public static class PointGrassCommon {
        // Grass meshes
        public static Mesh grassMeshFlat;
        public static Mesh grassMeshCyl;
        public static bool BladeMeshesGenerated { get { return grassMeshFlat != null && grassMeshCyl != null; } }
        // Shader Property IDs
        public static int ID_PointBuff;
        public static int ID_ObjBuff, ID_ObjCount;
        public static int ID_MatrixL2W, ID_MatrixW2L;
        public static bool PropertyIDsInitialized { get; private set; }
        private static ComputeBuffer inactiveDisplacementBuffer;

        // TerrainData.GetHeight crosses into the engine for every sample. Terrain
        // point generation touches millions of vertices, so read the normalized
        // heightmap once per terrain tile and sample it from managed memory.
        private static float[,] cachedHeights;

        // Metal requires every StructuredBuffer declared by the active shader variant to
        // be bound, even when its count is zero. A single shared, zero-strength object
        // keeps the optional displacement path valid when no manager is in the scene.
        private static ComputeBuffer InactiveDisplacementBuffer {
            get {
                if (inactiveDisplacementBuffer == null || !inactiveDisplacementBuffer.IsValid()) {
                    inactiveDisplacementBuffer = new ComputeBuffer(1, ObjectData.stride);
                    inactiveDisplacementBuffer.SetData(new[] { new ObjectData(Vector3.zero, 0f, 0f) });
                }
                return inactiveDisplacementBuffer;
            }
        }


        /// <summary>Finds shader property IDs</summary>
        public static void FindPropertyIDs() {
            if (PropertyIDsInitialized) { return; }

            ID_PointBuff = Shader.PropertyToID("_MeshPoints");
            ID_ObjBuff = Shader.PropertyToID("_DisplacementObjects");
            ID_ObjCount = Shader.PropertyToID("_DisplacementCount");

            ID_MatrixL2W = Shader.PropertyToID("_ObjMatrix_L2W");
            ID_MatrixW2L = Shader.PropertyToID("_ObjMatrix_W2L");

            PropertyIDsInitialized = true;
        }
        /// <summary>Updates a material property block's transformation matrices and any displacement data</summary>
        /// <param name="block">The referenced material property block</param>
        /// <param name="trans">The transform used for updating transformation matrices</param>
        public static void UpdateMaterialPropertyBlock(ref MaterialPropertyBlock block, Transform trans) {
            if (block == null || trans == null) { return; }
            // Update the matrices
            block.SetMatrix(ID_MatrixL2W, trans.localToWorldMatrix);
            block.SetMatrix(ID_MatrixW2L, trans.worldToLocalMatrix);
            // Update the object buffer
            if (PointGrassDisplacementManager.instance != null) {
                PointGrassDisplacementManager.instance.UpdatePropertyBlock(ref block);
            }
            else {
                block.SetBuffer(ID_ObjBuff, InactiveDisplacementBuffer);
                block.SetInt(ID_ObjCount, 0);
            }
        }

        /// <summary>Generates the flat and cylindrical grass blades (Stored in <c>grassMeshFlat</c> and <c>grassMeshCyl</c>)</summary>
        public static void GenerateGrassMeshes() {
            grassMeshFlat = GenerateGrassMesh_Flat();
            grassMeshCyl = GenerateGrassMesh_Cylinder();
        }
        /// <summary> Generates a grass blade mesh </summary>
        /// <param name="divisions">The number of divisions between the root and tip</param>
        private static Mesh GenerateGrassMesh_Flat(int divisions = 3) {
            // Ensures at least one triangle is generated
            divisions = Mathf.Max(divisions, 0);
            int vertCount = divisions * 2 + 3;
            int triCount = divisions * 2 + 1;

            Vector3[] verts = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            Color[] cols = new Color[vertCount];
            // Calculate the vertex positions
            float divisor = divisions + 1f;
            for (int i = 0; i < vertCount; i++) {
                float height = (i >> 1) / divisor;
                float radius = Mathf.Cos(height * Mathf.PI * 0.5f);
                if (i % 2 == 1) { radius = -radius; }

                Vector3 pos = new Vector3(radius, height, 0f);
                Vector2 uv = new Vector2(radius * 0.5f + 0.5f, height);

                verts[i] = pos;
                uvs[i] = uv;
                cols[i] = new Color(1f, 1f, 1f, uv.y);
            }
            // Create the inner triangles
            int[] tris = new int[triCount * 3];
            int vertPnt = 0;
            for (int i = 0; i < divisions; i++) {
                int a = i * 6;

                tris[a] = vertPnt;
                tris[a + 1] = vertPnt + 1;
                tris[a + 2] = vertPnt + 3;

                tris[a + 3] = vertPnt;
                tris[a + 4] = vertPnt + 3;
                tris[a + 5] = vertPnt + 2;

                vertPnt += 2;
            }
            // Cap the blade off
            int b = divisions * 6;
            tris[b] = vertPnt;
            tris[b + 1] = vertPnt + 1;
            tris[b + 2] = vertPnt + 2;

            Mesh mesh = new Mesh() {
                name = "Generated Grass Blade",
                vertices = verts,
                triangles = tris,
                colors = cols
            };
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();

            return mesh;
        }
        /// <summary>Generates a cylindrical grass blade mesh</summary>
        /// <param name="divisions">The number of vertices around the central axis</param>
        /// <param name="loops">The number of loops from the root to the tip</param>
        private static Mesh GenerateGrassMesh_Cylinder(int divisions = 3, int loops = 4) {
            int vertCount = divisions * loops + 1;
            Vector3[] verts = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            Color[] cols = new Color[vertCount];

            void SetVert(int index, Vector3 pos) {
                Vector2 uv = new Vector2(pos.x * 0.5f + 0.5f, pos.y);

                verts[index] = pos;
                uvs[index] = uv;
                cols[index] = new Color(1f, 1f, 1f, pos.y);
            }

            for (int i = 0; i < loops; i++) {
                float height = i / (float)loops;
                float radius = Mathf.Cos(height * Mathf.PI * 0.5f);
                int loopOffset = i * divisions;
                for (int j = 0; j < divisions; j++) {
                    float angle = (j / (float)divisions) * Mathf.PI * 2f;
                    Vector3 pos = new Vector3(
                        Mathf.Sin(angle) * radius,
                        height,
                        Mathf.Cos(angle) * radius
                    );
                    SetVert(loopOffset + j, pos);
                }
            }
            SetVert(vertCount - 1, Vector3.up);

            int triCount = (divisions * (loops - 1) * 2) + divisions;
            int[] tris = new int[triCount * 3];
            int triCounter = 0;
            for (int i = 0; i < loops - 1; i++) {
                for (int j = 0; j < divisions; j++) {
                    int baseVert = i * divisions + j;
                    int topVert = baseVert + divisions;
                    int rightVert = i * divisions + ((j + 1) % divisions);
                    int diagVert = rightVert + divisions;
                    // Tri A
                    tris[triCounter] = baseVert;
                    tris[triCounter + 1] = topVert;
                    tris[triCounter + 2] = rightVert;
                    // Tri B
                    tris[triCounter + 3] = rightVert;
                    tris[triCounter + 4] = topVert;
                    tris[triCounter + 5] = diagVert;

                    triCounter += 6;
                }
            }

            for (int i = 0; i < divisions; i++) {
                int baseVert = (divisions * (loops - 1)) + i;
                int rightVert = (divisions * (loops - 1)) + ((i + 1) % divisions);
                int topVert = vertCount - 1;

                tris[triCounter] = baseVert;
                tris[triCounter + 1] = topVert;
                tris[triCounter + 2] = rightVert;

                triCounter += 3;
            }

            Mesh mesh = new Mesh() {
                name = "Generated Cylindrical Grass Blade",
                vertices = verts,
                triangles = tris,
                colors = cols
            };
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>Projects the base mesh using its vertices and normals</summary>
        /// <param name="mesh">The local-space mesh to be projected</param>
        /// <param name="mask">The layer mask raycasts will use for collision</param>
        /// <param name="transform">The renderer's transform</param>
        public static void ProjectBaseMesh(ref MeshData mesh, LayerMask mask, Transform transform) {
            for (int i = 0; i < mesh.verts.Length; i++) {
                Matrix4x4 L2W = transform.localToWorldMatrix;
                Matrix4x4 W2L = transform.worldToLocalMatrix;

                Vector3 worldPos = L2W.MultiplyPoint(mesh.verts[i]);
                Vector3 worldNormal = L2W.MultiplyVector(mesh.normals[i]);

                if (Physics.Raycast(worldPos, -worldNormal, out RaycastHit hitInfo, 10f, mask)) {
                    Vector3 localPos = W2L.MultiplyPoint(hitInfo.point);
                    Vector3 localNormal = W2L.MultiplyVector(hitInfo.normal);

                    mesh.verts[i] = localPos;
                    mesh.normals[i] = localNormal;
                }
            }
            // Recalculate the mesh's bounding box
            mesh.RecalculateBounds();
        }

        // Static parameters used for storing terrain data
        // TODO : Point Grass Common - Expand the terrain data cache in preparation for multi-threaded generation
        private static int heightmapSize, alphamapWidth, alphamapHeight, numLayers;
        private static Vector3 terrainSize;
        private static float[,,] alphamaps;
        private static bool[] grassLayerMask;
        private static Texture2D[] layerTextures;
        private static Color32[][] layerTexturePixels;
        private static int[] layerTextureWidths, layerTextureHeights;
        private static TerrainLayer[] cachedTerrainLayers;
        private static Vector2[] layerOffsets, layerSizes;
        private static Color[] layerDiffuseRemapMins, layerDiffuseRemapMaxs;

        /// <summary>
        /// Samples the same weighted diffuse colour that Unity's terrain material uses at
        /// a local terrain position.  This is deliberately evaluated at the generated
        /// blade position, rather than interpolated from the terrain mesh vertices, so
        /// fine tiled detail remains continuous where a blade meets the ground.
        /// </summary>
        internal static Color SampleCachedTerrainColor(Vector3 localPosition) {
            if (layerTextures == null || terrainSize.x <= 0f || terrainSize.z <= 0f) { return Color.white; }

            Vector2 terrainUV = new Vector2(
                Mathf.Clamp01(localPosition.x / terrainSize.x),
                Mathf.Clamp01(localPosition.z / terrainSize.z));
            Color terrainColor = Color.black;

            for (int i = 0; i < numLayers; i++) {
                float alpha = SampleAlphamapBilinear(terrainUV, i);
                if (alpha <= 0f || layerTextures[i] == null) { continue; }

                Vector2 tileSize = layerSizes[i];
                if (tileSize.x <= 0f || tileSize.y <= 0f) { continue; }

                Vector2 textureUV = new Vector2(localPosition.x / tileSize.x, localPosition.z / tileSize.y) + layerOffsets[i];
                Color diffuse = SampleLayerTextureBilinear(i, textureUV);
                // Terrain diffuse remaps use the sampled colour as a per-channel
                // interpolation factor, rather than one scalar interpolation value.
                Color remapMin = layerDiffuseRemapMins[i];
                Color remapMax = layerDiffuseRemapMaxs[i];
                diffuse = new Color(
                    Mathf.Lerp(remapMin.r, remapMax.r, diffuse.r),
                    Mathf.Lerp(remapMin.g, remapMax.g, diffuse.g),
                    Mathf.Lerp(remapMin.b, remapMax.b, diffuse.b),
                    Mathf.Lerp(remapMin.a, remapMax.a, diffuse.a));
                terrainColor += diffuse * alpha;
            }

            terrainColor.a = 1f;
            return terrainColor;
        }

        private static Color SampleLayerTextureBilinear(int layer, Vector2 uv) {
            Color32[] pixels = layerTexturePixels[layer];
            int width = layerTextureWidths[layer];
            int height = layerTextureHeights[layer];
            if (pixels == null || width <= 0 || height <= 0) { return Color.white; }

            float x = Mathf.Repeat(uv.x, 1f) * (width - 1);
            float y = Mathf.Repeat(uv.y, 1f) * (height - 1);
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = (x0 + 1) % width;
            int y1 = (y0 + 1) % height;
            float tx = x - x0;
            float ty = y - y0;

            Color bottom = Color.Lerp((Color)pixels[y0 * width + x0], (Color)pixels[y0 * width + x1], tx);
            Color top = Color.Lerp((Color)pixels[y1 * width + x0], (Color)pixels[y1 * width + x1], tx);
            return Color.Lerp(bottom, top, ty);
        }

        private static float SampleAlphamapBilinear(Vector2 uv, int layer) {
            float x = uv.x * (alphamapWidth - 1);
            float y = uv.y * (alphamapHeight - 1);
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = Mathf.Min(x0 + 1, alphamapWidth - 1);
            int y1 = Mathf.Min(y0 + 1, alphamapHeight - 1);
            float tx = x - x0;
            float ty = y - y0;

            float bottom = Mathf.Lerp(alphamaps[y0, x0, layer], alphamaps[y0, x1, layer], tx);
            float top = Mathf.Lerp(alphamaps[y1, x0, layer], alphamaps[y1, x1, layer], tx);
            return Mathf.Lerp(bottom, top, ty);
        }

        /// <summary>
        /// Creates a readable, linear copy of a terrain layer at its native resolution.
        /// Keeping the original resolution is essential: these samples are stored on the
        /// grass instances and must match the terrain shader's albedo at the same location.
        /// </summary>
        private static Texture2D CreateTerrainTextureCopy(Texture2D source) {
            int width = Mathf.Max(source.width, 16);
            int height = Mathf.Max(source.height, 16);
            // Create a temporary render texture
            RenderTexture tmp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            // Get a reference to the active render texture
            RenderTexture previous = RenderTexture.active;
            // Blit performs the same sRGB-to-linear conversion used by the terrain shader.
            Graphics.Blit(source, tmp);
            // Keep this copy linear so GetPixelBilinear returns shader-ready albedo values.
            Texture2D outputTex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            // Read the pixels from the render texture into the output texture
            outputTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            outputTex.Apply();
            // Revert the active render texture
            RenderTexture.active = previous;
            // Release the temporary render texture
            RenderTexture.ReleaseTemporary(tmp);
            // Return the output texture
            return outputTex;
        }

        /// <summary>Caches terrain data for use with <c>PointGrassCommon.CreateMeshFromTerrainData()</c></summary>
        /// <param name="terrain">The terrain data that is cached</param>
        /// <param name="grassLayers">The target layers that will be used for distributing grass</param>
        public static void CacheTerrainData(TerrainData terrain, TerrainLayer[] grassLayers) {
            heightmapSize = terrain.heightmapResolution;
            terrainSize = terrain.size;
            cachedHeights = terrain.GetHeights(0, 0, heightmapSize, heightmapSize);

            alphamapWidth = terrain.alphamapWidth;
            alphamapHeight = terrain.alphamapHeight;
            numLayers = terrain.alphamapLayers;
            alphamaps = terrain.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

            TerrainLayer[] terrainLayers = terrain.terrainLayers;
            bool rebuildTextureCopies = layerTextures == null || cachedTerrainLayers == null ||
                                        cachedTerrainLayers.Length != terrainLayers.Length ||
                                        numLayers != terrainLayers.Length;
            if (!rebuildTextureCopies) {
                for (int i = 0; i < terrainLayers.Length; i++) {
                    if (cachedTerrainLayers[i] != terrainLayers[i]) {
                        rebuildTextureCopies = true;
                        break;
                    }
                }
            }

            if (rebuildTextureCopies && layerTextures != null) {
                for (int i = 0; i < layerTextures.Length; i++) {
                    if (layerTextures[i] == null) { continue; }
                    if (Application.isPlaying) Object.Destroy(layerTextures[i]);
                    else Object.DestroyImmediate(layerTextures[i]);
                }
            }

            grassLayerMask = new bool[numLayers];
            if (rebuildTextureCopies) {
                layerTextures = new Texture2D[numLayers];
                layerTexturePixels = new Color32[numLayers][];
                layerTextureWidths = new int[numLayers];
                layerTextureHeights = new int[numLayers];
                cachedTerrainLayers = (TerrainLayer[])terrainLayers.Clone();
            }
            layerOffsets = new Vector2[numLayers];
            layerSizes = new Vector2[numLayers];
            layerDiffuseRemapMins = new Color[numLayers];
            layerDiffuseRemapMaxs = new Color[numLayers];
            for (int i = 0; i < numLayers; i++) {
                grassLayerMask[i] = grassLayers.Contains(terrainLayers[i]);

                if (rebuildTextureCopies) {
                    layerTextures[i] = CreateTerrainTextureCopy(terrainLayers[i].diffuseTexture);
                    layerTexturePixels[i] = layerTextures[i].GetPixels32();
                    layerTextureWidths[i] = layerTextures[i].width;
                    layerTextureHeights[i] = layerTextures[i].height;
                }
                layerOffsets[i] = terrain.terrainLayers[i].tileOffset;
                layerSizes[i] = terrain.terrainLayers[i].tileSize;
                layerDiffuseRemapMins[i] = terrain.terrainLayers[i].diffuseRemapMin;
                layerDiffuseRemapMaxs[i] = terrain.terrainLayers[i].diffuseRemapMax;
            }
        }
        /// <summary>Creates a mesh from the cached terrain data. Terrain data is cached using <c>PointGrassCommon.CacheTerrainData()</c></summary>
        /// <param name="terrain">The target terrain data. Used for <c>TerrainData.GetHeight()</c></param>
        /// <param name="densityCutoff">The cutoff value for density</param>
        /// <param name="startX">The starting X-coord</param>
        /// <param name="startY">The starting Y-coord</param>
        /// <param name="sizeX">The number of vertices along X</param>
        /// <param name="sizeY">The number of vertices along Y</param>
        /// <returns><c>MeshData</c> - A piece of the terrain with position, triangle, normal, uv and color data for distributing point grass</returns>
        public static MeshData CreateMeshFromTerrainData(TerrainData terrain, float densityCutoff, int startX, int startY, int sizeX, int sizeY) {
            int vertCount = sizeX * sizeY;
            Vector3[] vertices = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            Color[] colors = new Color[vertCount];
            Vector2[] attrs = new Vector2[vertCount];

            int triCount = (sizeX - 1) * (sizeY - 1) * 2;
            int[] tris = new int[triCount * 3];

            float maskDivisor = 1f - densityCutoff;

            // Vert Calculation
            Vector2 uv = Vector2.zero;
            for (int x = 0; x < sizeX; x++) {
                int terrainSpaceX = x + startX;
                uv.x = Mathf.Clamp01((float)terrainSpaceX / (heightmapSize - 1));

                for (int y = 0; y < sizeY; y++) {
                    int terrainSpaceY = y + startY;
                    uv.y = Mathf.Clamp01((float)terrainSpaceY / (heightmapSize - 1));
                    // Calculcate the vertex index
                    int vertexIndex = x + y * sizeX;
                    // Get the local-space position of the vertex
                    Vector3 vertPos = new Vector3(uv.x, 0f, uv.y);
                    vertPos.Scale(terrainSize);
                    vertPos.y = cachedHeights[terrainSpaceY, terrainSpaceX] * terrainSize.y;
                    // Calculate the grass density mask. Per-blade colour is sampled after
                    // distribution, so doing the same texture work for every source vertex
                    // here would be redundant.
                    float grassMask = 0f;
                    // Get the pixel coordinate of the vertex
                    int coordX = Mathf.FloorToInt(uv.x * (alphamapWidth - 1));
                    int coordY = Mathf.FloorToInt(uv.y * (alphamapHeight - 1));
                    // For each layer on the terrain
                    for (int i = 0; i < numLayers; i++) {
                        float alphamapVal = alphamaps[coordY, coordX, i];
                        if (grassLayerMask[i]) { grassMask += alphamapVal; }
                    }
                    // Apply the density cutoff to the density mask
                    if (maskDivisor <= 0f) { grassMask = 0f; }
                    else { grassMask = Mathf.Clamp01((grassMask - densityCutoff) / maskDivisor); }
                    float bladeDensity = grassMask;
                    float bladeHeight = grassMask;

                    vertices[vertexIndex] = vertPos;
                    uvs[vertexIndex] = new Vector2(vertPos.x, vertPos.z);
                    attrs[vertexIndex] = new Vector2(bladeDensity, bladeHeight);
                    colors[vertexIndex] = Color.white;
                }
            }

            // Tri index calculation
            for (int x = 0; x < sizeX - 1; x++) {
                for (int y = 0; y < sizeY - 1; y++) {
                    // Calculate the vertex index
                    int index = x + y * sizeX;
                    // Calculate the base triangle index
                    int triIndex = (x + y * (sizeX - 1)) * 6;

                    tris[triIndex] = index;
                    tris[triIndex + 1] = index + sizeX;       // +1 Y
                    tris[triIndex + 2] = index + 1;           // +1 X

                    tris[triIndex + 3] = index + sizeX;       // +1 Y
                    tris[triIndex + 4] = index + sizeX + 1;   // +1 Y +1 X
                    tris[triIndex + 5] = index + 1;           // +1 X
                }
            }

            MeshData output = new MeshData(vertices, normals, uvs, colors, tris, attrs);
            output.pointColorSampler = SampleCachedTerrainColor;
            output.RecalculateNormals();
            return output;
        }

        /// <summary>Creates mesh data from an array of mesh filters</summary>
        /// <param name="parent">The parent object. More specifically, the transform used for converting the vertex's world position into local-space</param>
        /// <param name="filters">An array of type <c>MeshFilter</c> used to create the mesh data</param>
        /// <returns><c>MeshData</c> - The mesh created from the filters</returns>
        public static MeshData CreateMeshFromFilters(Transform parent, MeshFilter[] filters) {
            // Count the total number of vertices and triangle indices
            int totalVertCount = 0;
            int totalTriCount = 0;
            for (int i = 0; i < filters.Length; i++) {
                if (filters[i] != null && filters[i].sharedMesh != null) {
                    totalVertCount += filters[i].sharedMesh.vertexCount;
                    totalTriCount += filters[i].sharedMesh.triangles.Length;
                }
            }

            Vector3[] vertsArr = new Vector3[totalVertCount];
            Vector3[] normalsArr = new Vector3[totalVertCount];
            Vector2[] UVsArr = new Vector2[totalVertCount];
            Vector2[] attrsArr = new Vector2[totalVertCount];
            int[] trisArr = new int[totalTriCount];

            int processedVertCount = 0;
            int processedTriCount = 0;
            // Append all the mesh data
            for (int i = 0; i < filters.Length; i++) {
                if (filters[i] != null) {
                    Mesh filterMesh = filters[i].sharedMesh;
                    if (filterMesh != null) {
                        int meshVertCount = filterMesh.vertexCount;
                        int meshTriCount = filterMesh.triangles.Length;
                        Transform filterTransform = filters[i].transform;
                        // Add positions and normals
                        for (int j = 0; j < meshVertCount; j++) {
                            Vector3 newPos = parent.InverseTransformPoint(filterTransform.TransformPoint(filterMesh.vertices[j]));
                            Vector3 newNormal = parent.InverseTransformVector(filterTransform.TransformDirection(filterMesh.normals[j]));

                            vertsArr[processedVertCount + j] = newPos;
                            normalsArr[processedVertCount + j] = newNormal;
                        }
                        // Add mesh attributes
                        if (filterMesh.colors != null && filterMesh.colors.Length == meshVertCount) {
                            for (int j = 0; j < meshVertCount; j++) {
                                attrsArr[processedVertCount + j] = 
                                    new Vector2(filterMesh.colors[i].r, filterMesh.colors[i].g);
                            }
                        }
                        // Fill attributes with empty values (1, 1)
                        else { for (int j = 0; j < meshVertCount; j++) { attrsArr[processedVertCount + j] = Vector2.one; } }
                        // Copy the mesh's UVs to the combined UVs array, accounting for the vertex offsets
                        System.Array.Copy(filterMesh.uv, 0, UVsArr, processedVertCount, meshVertCount);
                        // Fill the triangles array while applying the index offset
                        for (int j = 0; j < meshTriCount; j++) {
                            trisArr[processedTriCount + j] = 
                                filterMesh.triangles[j] + processedVertCount;
                        }

                        processedVertCount += meshVertCount;
                        processedTriCount += meshTriCount;
                    }
                }
            }
            // Return the final mesh data
            MeshData mesh = new MeshData(vertsArr, normalsArr, UVsArr, trisArr, attrsArr);
            mesh.RecalculateBounds();
            return mesh;
        }



        public enum BladeType { Flat, Cylindrical, Mesh }

        [System.Serializable]
        public struct MeshPoint {
            public Vector3 position;
            public Vector3 normal;
            // TODO : Point Grass Common (MeshPoint) - Look into only using 8 bits per colour channel
            public Color color;         // Reserved for colour sampling
            public Vector4 extraData;   // XY = Mesh UV, Z = Blade Length, W = Random Value 0 -> 1

            // pos = 12, normal = 12, color = 16, extra = 16
            // 12 + 12 + 16 + 16 = 56 bytes
            public const int stride = 56;

            public MeshPoint(Vector3 position, Vector3 normal, Color color, Vector4 extraData) {
                this.position = position;
                this.normal = normal;
                this.color = color;
                this.extraData = extraData;
            }
        }

        [System.Serializable]
        public struct ObjectData {
            public Vector3 position;
            public float radius;
            public float strength;

            // pos = 12, radius = 4, strength = 4
            // 12 + 4 + 4 = 20 bytes
            public const int stride = 20;

            public ObjectData(Vector3 position, float radius, float strength) {
                this.position = position;
                this.radius = radius;
                this.strength = strength;
            }
        }

        public class MeshData {
            public Vector3[] verts;
            public Vector3[] normals;
            public Vector2[] UVs;
            // TODO : Point Grass Common (MeshData) - Look into only using 8 bits per colour channel
            public Color[] colours;
            // TODO : Point Grass Common (MeshData) - May want to implement 16-bit indices on mesh data to save some memory space?
            public int[] tris;
            public Vector2[] attributes;

            /// <summary>Optional exact colour sampler used after a random point has been placed.</summary>
            public System.Func<Vector3, Color> pointColorSampler;

            public Bounds bounds;

            public MeshData(Vector3[] verts, Vector3[] normals, Vector2[] UVs, Color[] colours, int[] tris, Vector2[] attributes) {
                this.verts = verts;
                this.normals = normals;
                this.UVs = UVs;
                this.colours = colours;
                this.tris = tris;
                this.attributes = attributes;

                bounds = new Bounds();
                RecalculateBounds();
            }
            public MeshData(Vector3[] verts, Vector3[] normals, Vector2[] UVs, int[] tris, Vector2[] attributes) {
                this.verts = verts;
                this.normals = normals;
                this.UVs = UVs;
                this.tris = tris;
                this.attributes = attributes;

                colours = null;

                bounds = new Bounds();
                RecalculateBounds();
            }
            public MeshData(Vector3[] verts, Vector3[] normals, Vector2[] UVs, int[] tris) {
                this.verts = verts;
                this.normals = normals;
                this.UVs = UVs;
                this.tris = tris;

                colours = null;
                attributes = null;

                bounds = new Bounds();
                RecalculateBounds();
            }

            public bool HasColours => colours != null && colours.Length > 0;
            public bool HasAttributes => attributes != null && attributes.Length > 0;

            public void RecalculateBounds() {
                if (verts == null || verts.Length < 2) { return; }

                bounds = new Bounds(verts[0], Vector3.zero);
                for (int i = 1; i < verts.Length; i++) { bounds.Encapsulate(verts[i]); }
            }
            public void RecalculateNormals() {
                System.Array.Clear(normals, 0, normals.Length);

                for (int i = 0; i < tris.Length; i += 3) {
                    Vector3 offsetB = verts[tris[i + 1]] - verts[tris[i]];
                    Vector3 offsetC = verts[tris[i + 2]] - verts[tris[i]];

                    Vector3 normal = Vector3.Normalize(Vector3.Cross(offsetB, offsetC));
                    for (int j = 0; j < 3; j++) { normals[tris[i + j]] += normal; }
                }
                for (int i = 0; i < normals.Length; i++) { normals[i].Normalize(); }
            }

            public void ApplyDensityCutoff(float cutoff) {
                if (cutoff <= 0f) { return; } // If the cutoff is below 0, we don't need to do anything
                else if (cutoff >= 1f) { // If the cutoff is >= 1f, don't do anything to prevent a divide by zero error. Also log an error
                    Debug.LogError("Point Grass Common - An attempt was made to apply a density cutoff greater than or equal to 1f. This would have caused an error, so no cutoff was applied"); return;
                }
                // For each vertex, apply the cutoff for the attribute data
                float divisor = (1f - cutoff); // Will cause issues if the cutoff is 1.0
                for (int i = 0; i < attributes.Length; i++) {
                    attributes[i].x = Mathf.Clamp01(attributes[i].x - cutoff) / divisor;
                }
            }
            public void ApplyLengthMapping(Vector2 mapping) {
                mapping.x = Mathf.Clamp01(mapping.x);
                mapping.y = mapping.y < mapping.x ? mapping.x : Mathf.Clamp01(mapping.y);
                // For each vertex, apply the length mapping for the attribute data
                for (int i = 0; i < attributes.Length; i++) {
                    attributes[i].y = Mathf.Lerp(mapping.x, mapping.y, attributes[i].y);
                }
            }

            public static readonly MeshData Empty = new MeshData(null, null, null, null, null);
        }

        public enum DistributionSource { Mesh, MeshFilter, TerrainData, SceneFilters }
        public enum ProjectionType { None, ProjectMesh }
    }
}
