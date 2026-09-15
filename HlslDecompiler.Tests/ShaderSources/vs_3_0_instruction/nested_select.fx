float4 k : register(c4);
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
};

struct VS_OUT
{
	float4 position : POSITION;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float2 r0;
	float r1;
	o.position.x = dot(i.position, transpose(wvp)[0]);
	o.position.y = dot(i.position, transpose(wvp)[1]);
	o.position.z = dot(i.position, transpose(wvp)[2]);
	o.position.w = dot(i.position, transpose(wvp)[3]);
	r0 = (i.color.xy < 0) ? 1 : 0;
	r1 = lerp(k.y, k.x, r0.y);
	r0.y = r1.x + -k.z;
	o.color.x = r0.x * r0.y + k.z;
	r0 = max(k.xy, i.color.xy);
	o.color.y = min(r0.y, r0.x);
	r0.x = (i.color.z >= k.z) ? 1 : 0;
	r0.y = lerp(k.x, k.y, i.color.w);
	o.color.z = r0.y * r0.x;
	r0.x = -i.color.y + i.color.x;
	r0.y = k.w + -i.color.z;
	o.color.w = r0.y * -r0.x;

	return o;
}
