sampler2D normals;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	half3 texcoord1 : TEXCOORD1;
	half3 texcoord2 : TEXCOORD2;
	half3 texcoord3 : TEXCOORD3;
};

float4 main(PS_IN i) : COLOR
{
	float3 t0 = tex2D(normals, i.texcoord).xyz;
	half3 t1 = 2 * t0.zxy - 1;
	half3 t2 = t1.x * i.texcoord3 + (half3)(t1.y * i.texcoord1 + (half3)(t1.z * i.texcoord2));
	return float4(t2, 1);
}
