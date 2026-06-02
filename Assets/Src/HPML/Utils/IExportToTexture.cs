using Unity.Jobs;
using UnityEngine;

namespace HPML
{
    public interface IExportToTexture
    {
        public JobHandle ToTexture2D(Texture2D texture, JobHandle dependsOn);
        public void ToTexture2D(Texture2D texture);
    }
}