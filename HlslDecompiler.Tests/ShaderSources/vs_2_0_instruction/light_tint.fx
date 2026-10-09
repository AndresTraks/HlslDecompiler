float3 eye : register(c7);
float4 fogParams : register(c9);
float3 lightDir : register(c8);
float4x4 w : register(c4);
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
};

struct VS_OUT
{
	float4 color : COLOR;
	float fog : FOG;
	float4 position : POSITION;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float3 r0;
	float3 r1;
	o.position.x = dot(i.position, transpose(wvp)[0]);
	o.position.y = dot(i.position, transpose(wvp)[1]);
	o.position.z = dot(i.position, transpose(wvp)[2]);
	o.position.w = dot(i.position, transpose(wvp)[3]);
	r0.x = dot(i.normal.xyz, transpose(w)[0].xyz);
	r0.y = dot(i.normal.xyz, transpose(w)[1].xyz);
	r0.z = dot(i.normal.xyz, transpose(w)[2].xyz);
	r1 = normalize(r0.xyz);
	r0.x = dot(r1.xyz, -lightDir.xyz);
	r0.x = max(r0.x, 0);
	r0.x = min(r0.x, 1);
	o.color = r0.x * float4(1, 0.9, 0.8, 1);
	r0.x = dot(i.position, transpose(w)[0]);
	r0.y = dot(i.position, transpose(w)[1]);
	r0.z = dot(i.position, transpose(w)[2]);
	r0 = r0.xyz + -eye.xyz;
	r0.x = dot(r0.xyz, r0.xyz);
	r0.x = rsqrt(r0.x);
	r0.x = 1 / r0.x;
	r0.x = -r0.x + fogParams.y;
	r0.x = r0.x * fogParams.z;
	r0.x = max(r0.x, 0);
	o.fog = min(r0.x, 1);

	return o;
}
