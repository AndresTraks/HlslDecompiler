struct PS_IN
{
	float4 color : COLOR;
	float depth : DEPTH;
};

interface IFog
{
	float4 F0(float4 color, float depth);
};

class Lin : IFog
{
	float4 m0;
	float2 m1;

	float4 F0(float4 color, float depth)
	{
		return lerp(color, m0, saturate((depth - m1.x) / m1.y));
	}
};

class Exp : IFog
{
	float m0;
	float3 pad0;
	float4 m1;

	float4 F0(float4 color, float depth)
	{
		return lerp(m1, color, exp(-depth * m0));
	}
};

IFog fog;

float4 main(PS_IN i) : SV_Target
{
	return fog.F0(i.color, i.depth);
}
