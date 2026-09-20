float4x4 world;
float4x4 viewProj;
float4 clipPlane;
float3 lightDir;

struct VS_IN
{
	float4 position : POSITION;
	float3 normal : NORMAL;
	float3 tangent : TANGENT;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	float3 texcoord : TEXCOORD;
	float sv_clipdistance : SV_ClipDistance;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 t0 = mul(i.position, world);
	float3 t1 = mul(i.tangent, (float3x3)world);
	float3 t2 = normalize(t1);
	float3 t3 = mul((float3x3)world, i.normal);
	o.sv_position = mul(t0, viewProj);
	o.normal = normalize(t3);
	o.texcoord = float3(dot(lightDir, t2), dot(lightDir, cross(normalize(t3), t2)), dot(lightDir, normalize(t3)));
	o.sv_clipdistance = dot(clipPlane, t0);

	return o;
}
