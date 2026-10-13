// Matches EmergenceSimulation.Particle (72 bytes). Stable IDs survive spatial sorting.
struct Particle
{
    float2 position, velocity;
    int species;
    float radius, reserve, waste, maturity, disruption, cooldown, returnIn;
    int feedingPhase, feedingTarget;
    float transition, visualScale;
    float2 feedingStart;
};
struct Group
{
    uint size, motors, catalysts, viable;
    float reserve, waste;
    float2 center, velocity;
    uint mature, cooling;
    float2 swimming;
};
struct Influence
{
    float4 wake, drift, pulse;
    float4 swordCenter, swordX, swordY, swordZ, swordShape, attack;
    uint4 stamps;
};
struct Bud
{
    uint serial, phase, anchor, root;
    float2 position, velocity, direction;
    float elapsed, duration, cost, cancelFrom, retractElapsed, radius;
};
