struct PS_IN
{
	float3 normal : NORMAL;
	float4 texcoord : TEXCOORD;
};

interface IF
{
	float4 F0(float3 normal, float4 r0);
};

class A : IF
{
	float3 m0;

	float4 F0(float3 normal, float4 r0)
	{
		r0.yzw = normal.xyz * m0.xyz + r0.xxx;

		return r0;
	}
};

class B : IF
{
	float m0;

	float4 F0(float3 normal, float4 r0)
	{
		r0.yzw = normal.zyx * m0 + -(r0.xxx);

		return r0;
	}
};

IF f;

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	r0.x = i.texcoord.y * i.texcoord.x;
	r0 = f.F0(i.normal, r0);
	o.xyz = r0.yzw;
	o.w = 1;

	return o;
}
