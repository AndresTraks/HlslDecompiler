struct VS_OUT
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	float3 r0;
	float4 r1;
	float2 r2;
	r0 = float3(dot(position, float4(2, 3, 5, 7)), dot(position, float4(11, 13, 17, 19)), dot(position, float4(23, 29, 31, 37)));
	r1 = float4(dot(position.xyz, float3(41, 43, 47)), dot(position.xyz, float3(59, 61, 67)), dot(position.xyz, float3(73, 79, 83)), dot(position.xyz, float3(97, 101, 103)));
	r2 = float2(dot(position.xyz, float3(109, 113, 127)), dot(position.xyz, float3(137, 139, 149)));
	o.position.xyz = r0.xyz;
	o.position.w = r1.w;
	o.texcoord.xy = r2.xy;
	o.texcoord.zw = r1.xy;

	return o;
}
