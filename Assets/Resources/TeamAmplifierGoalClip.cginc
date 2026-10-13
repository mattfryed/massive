#ifndef MASSIVE_TEAM_GOAL_CLIP
#define MASSIVE_TEAM_GOAL_CLIP
float4 _GoalOrigin, _GoalRight, _GoalUp, _GoalShape;
float TeamGoalCoverage(float3 world)
{
    float3 delta = world - _GoalOrigin.xyz;
    float2 p = float2(dot(delta, _GoalRight.xyz) * _GoalRight.w, dot(delta, _GoalUp.xyz));
    float2 extent = max(_GoalShape.xy, .001);
    float k = _GoalShape.z;
    float2 q = float2(k + (1-k) * p.x / extent.x, sqrt(max(.0001, 1-k*k)) * p.y / extent.y);
    float distance = 1-length(q);
    return saturate(p.x / max(fwidth(p.x), .00001) + .5)
        * saturate((distance - _GoalShape.w / min(extent.x, extent.y)) / max(fwidth(distance), .00001) + .5);
}
#endif
