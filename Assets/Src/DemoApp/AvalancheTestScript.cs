using System;
using System.Collections;
using HPML;
using TFM.Components;
using Unity.Collections;
using UnityEngine;

public class AvalancheTestScript : MonoBehaviour
{
    private doubleF preAvalancheCopy;
    private doubleF cumulativeMeasurements;
    
    private void Start()
    {
        var sim = GetComponent<SimulationController>();
        preAvalancheCopy = new doubleF(sim.Heightfield, Allocator.Persistent);
        cumulativeMeasurements = new doubleF(sim.Heightfield, Allocator.Persistent);
    }
}