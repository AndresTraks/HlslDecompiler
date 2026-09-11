float4x4 viewProj;
float3 right;
float3 up;
float size;

struct VS_IN
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
};

float4 main(VS_IN i) : SV_Position
{
	float4 o;

	float4 r0;
	r0.xyz = i.texcoord.yyy * up.xyz;
	r0.xyz = right.xyz * i.texcoord.xxx + r0.xyz;
	r0.xyz = r0.xyz * size + i.position.xyz;
	r0.w = 1;
	o.x = dot(r0, transpose(viewProj)[0]);
	o.y = dot(r0, transpose(viewProj)[1]);
	o.z = dot(r0, transpose(viewProj)[2]);
	o.w = dot(r0, transpose(viewProj)[3]);

	return o;
}
