float4x3 bones[40];
float4 palette[16];
float4x4 viewProj;

struct VS_IN
{
	float4 position : POSITION;
	float4 blendweight : BLENDWEIGHT;
	float4 blendindices : BLENDINDICES;
	float4 blendindices1 : BLENDINDICES1;
};

struct VS_OUT
{
	float4 color : COLOR;
	float4 position : POSITION;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	int2 a0;
	float3 r1;
	float3 r2;
	r0 = i.blendindices.zyxw * 765.00586;
	a0.x = r0.y;
	r1.x = dot(i.position, transpose(bones[a0.x / 3])[0]);
	r1.y = dot(i.position, transpose(bones[a0.x / 3])[1]);
	r1.z = dot(i.position, transpose(bones[a0.x / 3])[2]);
	r1 = r1.xyz * i.blendweight.yyy;
	a0.x = r0.x;
	r2.x = dot(i.position, transpose(bones[a0.x / 3])[0]);
	r2.y = dot(i.position, transpose(bones[a0.x / 3])[1]);
	r2.z = dot(i.position, transpose(bones[a0.x / 3])[2]);
	r1 = r2.xyz * i.blendweight.xxx + r1.xyz;
	a0 = r0.zw;
	r0.x = dot(i.position, transpose(bones[a0.x / 3])[0]);
	r0.y = dot(i.position, transpose(bones[a0.x / 3])[1]);
	r0.z = dot(i.position, transpose(bones[a0.x / 3])[2]);
	r0.xyz = r0.xyz * i.blendweight.zzz + r1.xyz;
	r1.x = dot(i.position, transpose(bones[a0.y / 3])[0]);
	r1.y = dot(i.position, transpose(bones[a0.y / 3])[1]);
	r1.z = dot(i.position, transpose(bones[a0.y / 3])[2]);
	r0.xyz = r1.xyz * i.blendweight.www + r0.xyz;
	r0.w = 1;
	o.position.x = dot(r0, transpose(viewProj)[0]);
	o.position.y = dot(r0, transpose(viewProj)[1]);
	o.position.z = dot(r0, transpose(viewProj)[2]);
	o.position.w = dot(r0, transpose(viewProj)[3]);
	r0.x = i.blendindices1.y * 255.00195;
	a0.x = r0.x;
	o.color = palette[a0.x];

	return o;
}
