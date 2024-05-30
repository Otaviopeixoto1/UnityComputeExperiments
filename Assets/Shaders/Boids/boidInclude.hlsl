#ifndef BOIDS_H
#define BOIDS_H


struct BoidData
{
    float3 velocity; //Change to direction + speed on the padding float
    float pad0;
    float3 position;
    float pad1;
};

// Boids read-only structured buffer
StructuredBuffer<BoidData> boidsIn;

// Boids read-write structured buffer
RWStructuredBuffer<BoidData> boidsOut;


#endif 