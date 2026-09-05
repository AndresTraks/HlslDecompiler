float4x4 bones[4];
float4x4 viewProj;

struct VS_IN
{
	float4 position : POSITION;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(VS_IN i) : SV_Position
{
	float4 o;

	float4 r0;
	float4 r1;
	r0.x = dot(i.position, transpose(bones[1])[0]);
	r0.y = dot(i.position, transpose(bones[1])[1]);
	r0.z = dot(i.position, transpose(bones[1])[2]);
	r0.w = dot(i.position, transpose(bones[1])[3]);
	r0 = r0 * i.blendweight.y;
	r1.x = dot(i.position, transpose(bones[0])[0]);
	r1.y = dot(i.position, transpose(bones[0])[1]);
	r1.z = dot(i.position, transpose(bones[0])[2]);
	r1.w = dot(i.position, transpose(bones[0])[3]);
	r0 = r1 * i.blendweight.x + r0;
	o.x = dot(r0, transpose(viewProj)[0]);
	o.y = dot(r0, transpose(viewProj)[1]);
	o.z = dot(r0, transpose(viewProj)[2]);
	o.w = dot(r0, transpose(viewProj)[3]);

	return o;
}
