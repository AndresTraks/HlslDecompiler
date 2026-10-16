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
		float4 r1;
		float4 r2;
		float4 r0;
		r1.z = depth + -(m1.x);
		r1.z = saturate(r1.z / m1.y);
		r2 = -(color) + m0;
		r0 = r1.z * r2 + color;

		return r0;
	}
};

class Exp : IFog
{
	float m0;
	float3 pad0;
	float4 m1;

	float4 F0(float4 color, float depth)
	{
		float4 r1;
		float4 r2;
		float4 r0;
		r1.z = -(depth) * m0;
		r1.z = r1.z * 1.44269502;
		r1.z = exp2(r1.z);
		r2 = color + -(m1);
		r0 = r1.z * r2 + m1;

		return r0;
	}
};

IFog fog;

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	r0 = fog.F0(i.color, i.depth);
	o = r0;

	return o;
}
