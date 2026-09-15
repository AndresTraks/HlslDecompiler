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
	float t2 = length(t1);
	float3 t3 = t1.zyx / t2;
	float3 t4 = mul((float3x3)world, i.normal);
	float t5 = length(t4);
	o.sv_position = mul(t0, viewProj);
	o.normal = t4 / t5;
	o.texcoord = float3(dot(lightDir, t3.zyx), dot(lightDir, cross(t4 / t5, t3.zyx)), dot(lightDir, t4 / t5));
	o.sv_clipdistance = dot(clipPlane, t0);

	return o;
}
