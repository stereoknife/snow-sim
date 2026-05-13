using HPML;
using TFM.Simulation;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace TFM.Components.Solvers
{
    public class MonolithicSimulation
    {
        private Snow.Parameters _parameters;
        private doubleF _height;
        private doubleF _temperature;
        private double4F _snow;
        private double3F _wind;
        private doubleF _windAltitude;
        
        public Snow.Parameters parameters { set => _parameters = value; }
        public doubleF height { set => _height = value; }
        public doubleF temperature { set => _temperature = value; }
        public double4F snow { set => _snow = value; }
        public double3F wind { set => _wind = value; }
        public doubleF windAltitude { set => _windAltitude = value; }
        
        private Random _rng;
        private readonly uint _seed;
        
        private const float MeltStepLambda = 0.5f;
        private const float TransportStepLambda = 0.5f;
        private const float SnowfallStepLambda = 1f;
        private const float SnowfallStartLambda = 7f;
        private const float SnowfallEndLambda = 3f;
        private const float DiffusionStepLambda = 0.5f;
        
        private float _meltStepTimer, _transportStepTimer, _snowfallStepTimer, _snowfallStartTimer, _snowfallEndTimer, _diffusionStepTimer;
        private float _meltStepDt, _transportStepDt, _snowfallStepDt, _snowfallStartDt, _snowfallEndDt, _diffusionStepDt;
        
        private float Q(float p, float lambda) => -log(1 - p) / lambda;

        public MonolithicSimulation(uint seed)
        {
            _seed = seed;
            Reset();
        }

        public void Reset()
        {
            _rng = new Random(_seed);
            _meltStepTimer = _meltStepDt = Q(_rng.NextFloat(), MeltStepLambda);
            _transportStepTimer = _transportStepDt = Q(_rng.NextFloat(), TransportStepLambda);
            _diffusionStepTimer = _diffusionStepDt = Q(_rng.NextFloat(), DiffusionStepLambda);
            _snowfallStartTimer = _snowfallStartDt = Q(_rng.NextFloat(), SnowfallStartLambda);
            _snowfallStepTimer = _snowfallStepDt = float.PositiveInfinity;
            _snowfallEndTimer = _snowfallEndDt = float.PositiveInfinity;
        }

        public void FastForward()
        {
            var dt = min(min(_meltStepTimer, _transportStepTimer),
                min(min(_snowfallStepTimer, _snowfallStartTimer), min(_snowfallEndTimer, _diffusionStepTimer)));
            Step(dt);
        }
        
        public void Step(float dt)
        {
            _meltStepTimer -= dt;
            _transportStepTimer -= dt;
            _snowfallStepTimer -= dt;
            _snowfallStartTimer -= dt;
            _snowfallEndTimer -= dt;
            _diffusionStepTimer -= dt;

            JobHandle jh = new JobHandle();
            if (_meltStepTimer <= 0)
            {
                jh = Snow.Melt(_snow, _temperature, _height, _meltStepDt, ref _parameters, jh);
                _meltStepTimer = _meltStepDt = Q(_rng.NextFloat(), MeltStepLambda);
            }
            if (_transportStepTimer <= 0)
            {
                jh = Snow.Transport(_snow, _wind, _windAltitude, _height, _transportStepDt, ref _parameters, jh);
                _transportStepTimer = _transportStepDt = Q(_rng.NextFloat(), TransportStepLambda);
            }
            if (_diffusionStepTimer <= 0)
            {
                jh = Snow.Diffusion(_snow, _height, _diffusionStepDt, ref _parameters, jh);
                _diffusionStepTimer = _diffusionStepDt = Q(_rng.NextFloat(), DiffusionStepLambda);
            }
            if (_snowfallStepTimer <= 0)
            {
                jh = Snow.Snowfall(_snow, _height, _temperature, _snowfallStepDt, ref _parameters, jh);
                _snowfallStepTimer = _snowfallStepDt = Q(_rng.NextFloat(), SnowfallStepLambda);
            }
            if (_snowfallStartTimer <= 0)
            {
                _snowfallStartTimer = _snowfallStartDt = float.PositiveInfinity;
                _snowfallStepTimer = _snowfallStepDt = Q(_rng.NextFloat(), SnowfallStepLambda);
                _snowfallEndTimer = _snowfallEndDt = Q(_rng.NextFloat(), SnowfallEndLambda);
            }
            if (_snowfallEndTimer <= 0)
            {
                jh = Snow.Snowfall(_snow, _height, _temperature, _snowfallStepDt - _snowfallStepTimer, ref _parameters, jh);
                _snowfallStartTimer = _snowfallStartDt = Q(_rng.NextFloat(), SnowfallStartLambda);
                _snowfallStepTimer = _snowfallStepDt = float.PositiveInfinity;
                _snowfallEndTimer = _snowfallEndDt = float.PositiveInfinity;
            }
            jh.Complete();
        }
    }
}
