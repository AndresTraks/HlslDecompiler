float4 scales[4] : register(c4);
float4 threshold : register(c8);
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
	float4 texcoord : TEXCOORD;
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
	int a0;
	float4 r1;
	float2 r3;
	float4 r2;
	o.position.x = dot(i.position, transpose(wvp)[0]);
	o.position.y = dot(i.position, transpose(wvp)[1]);
	o.position.z = dot(i.position, transpose(wvp)[2]);
	o.position.w = dot(i.position, transpose(wvp)[3]);
	r0.x = (i.texcoord.x < -i.texcoord.x) ? 1 : 0;
	r0.y = frac(i.texcoord.x);
	r0.z = -r0.y + i.texcoord.x;
	r0.y = (-r0.y < r0.y) ? 1 : 0;
	r0.x = r0.x * r0.y + r0.z;
	a0 = r0.x;
	r0 = i.color * scales[a0];
	r1 = (i.color < threshold) ? 1 : 0;
	r3 = frac(i.color.zw);
	r2.zw = r3.xy;
	r2.xy = frac(i.color.xy);
	r1 = r1 * r2;
	r2 = (i.color >= threshold) ? 1 : 0;
	r0 = r2 * r0 + r1;
	r1.x = exp2(i.color.x);
	r0 = r0 + r1.x;
	r1.x = log2(i.color.y);
	o.color = r0 + r1.x;

	return o;
}
