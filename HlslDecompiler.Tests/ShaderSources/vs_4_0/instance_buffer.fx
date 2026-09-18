float4x4 viewProjection;

StructuredBuffer<float4x4> instances : register(t0);

struct VS_IN
{
	float4 position : POSITION;
	float3 normal : NORMAL;
	uint sv_instanceid : SV_InstanceID;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	float3 texcoord : TEXCOORD;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float t0 = dot(i.position, transpose(instances[i.sv_instanceid])[3]);
	float4 t1 = transpose(instances[i.sv_instanceid])[0];
	float4 t2 = transpose(instances[i.sv_instanceid])[1];
	float4 t3 = transpose(instances[i.sv_instanceid])[2];
	o.sv_position = mul(float4(dot(i.position, t1), dot(i.position, t2), dot(i.position, t3), t0), viewProjection);
	o.normal = float3(dot(i.normal, t1.xyz), dot(i.normal, t2.xyz), dot(i.normal, t3.xyz));
	o.texcoord = float3(dot(i.position, t1), dot(i.position, t2), dot(i.position, t3));

	return o;
}
