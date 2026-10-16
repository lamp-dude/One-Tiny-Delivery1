using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MicahW.PointGrass {
    using static PointGrassCommon;

    public class PointGrassDisplacementManager : MonoBehaviour {
        public static PointGrassDisplacementManager instance;

        private ComputeBuffer objectsBuffer;
        private List<PointGrassDisplacer> displacers;
        private ObjectData[] objectData;

        private static readonly int maxDisplacerCount = 32;
        private int DisplacerCount => Mathf.Min(displacers.Count, maxDisplacerCount);
        public int ActiveDisplacerCount => DisplacerCount;

        public delegate void DisplacementDelegate(PointGrassDisplacementManager manager);
        public static event DisplacementDelegate OnInitialize;

        private void Awake() {
            if (instance != null) { Destroy(this); }
            else {
                instance = this;

                objectsBuffer = new ComputeBuffer(maxDisplacerCount, ObjectData.stride);
                displacers = new List<PointGrassDisplacer>();
                objectData = new ObjectData[maxDisplacerCount];

                OnInitialize?.Invoke(this);
            }
        }
        private void OnDisable() {
            if (instance == this) {
                instance = null;

                objectsBuffer.Release();
                displacers.Clear();
            }
        }

        private void LateUpdate() { UpdateBuffer(); }

        // Update the compute buffer with the displacers' data
        private void UpdateBuffer() {
            int count = DisplacerCount;
            if (count == 0) { return; }
            for (int i = 0; i < count; i++) { objectData[i] = displacers[i].GetObjectData(); }
            objectsBuffer.SetData(objectData, 0, 0, count);
        }

        // Update functions to add and remove displacers from the internal list
        public void AddDisplacer(PointGrassDisplacer displacer) => displacers.Add(displacer);
        public void RemoveDisplacer(PointGrassDisplacer displacer) => displacers.Remove(displacer);
        
        // Public method to add the displacement buffer to the point grass shader
        public void UpdatePropertyBlock(ref MaterialPropertyBlock block) {
            // Set buffer data here
            block.SetBuffer(ID_ObjBuff, objectsBuffer);
            block.SetInt(ID_ObjCount, DisplacerCount);
        }
    }
}
