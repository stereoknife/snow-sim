using Sim.Structs;
using Unity.Collections;
using UnityEngine;

namespace Sim
{
    public class Terrain : MonoBehaviour
    {
        [SerializeField] private Texture2D heightmap;
        [SerializeField] private float sizeX;
        [SerializeField] private float sizeZ;
        [SerializeField] private float height;

        private ScalarField2D heightfield;
        public ScalarField2D Heightfield => heightfield;

        public void Init()
        {
            heightfield = ScalarField2D.FromTexture(heightmap, new(sizeX, height, sizeZ), Allocator.Persistent);
        }

        private void Update()
        {
            heightfield.size = new (sizeX, height, sizeZ);
        }

        private void OnDestroy()
        {
            heightfield.Dispose();
        }
    }
}
