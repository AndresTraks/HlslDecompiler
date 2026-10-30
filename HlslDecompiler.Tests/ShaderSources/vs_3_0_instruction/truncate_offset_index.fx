float4 pal[16] : register(c4);
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

struct VS_OUT
{
	float4 position : POSITION;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float3 r0;
	int a0;
	o.position.x = dot(i.position, transpose(wvp)[0]);
	o.position.y = dot(i.position, transpose(wvp)[1]);
	o.position.z = dot(i.position, transpose(wvp)[2]);
	o.position.w = dot(i.position, transpose(wvp)[3]);
	r0.x = 8 * i.texcoord.x;
	r0.x = frac(r0.x);
	r0.y = (-r0.x < r0.x) ? 1 : 0;
	r0.z = i.texcoord.x * 8 + -4;
	r0.x = r0.z + -r0.x;
	r0.z = (r0.z < -r0.z) ? 1 : 0;
	r0.x = r0.z * r0.y + r0.x;
	r0.x = max(r0.x, 0);
	r0.x = min(r0.x, 15);
	a0 = r0.x;
	o.color = pal[a0];

	return o;
}
