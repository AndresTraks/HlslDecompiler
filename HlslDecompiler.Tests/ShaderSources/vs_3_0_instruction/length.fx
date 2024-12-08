struct VS_IN
{
	float4 position : POSITION;
	float4 position1 : POSITION1;
	float4 position2 : POSITION2;
};

float4 main(VS_IN i) : POSITION
{
	float4 o;

	float4 r0;
	r0.x = dot(i.position, i.position);
	r0.x = 1 / sqrt(r0.x);
	o.x = 1 / r0.x;
	r0.x = dot(i.position1.xyz, i.position1.xyz);
	r0.x = 1 / sqrt(r0.x);
	o.y = 1 / r0.x;
	r0.xy = i.position2.xy * i.position2.xy;
	r0.x = r0.y + r0.x;
	r0.x = 1 / sqrt(r0.x);
	r0.x = 1 / r0.x;
	o.z = -r0.x;
	r0 = 2 + i.position2;
	r0.x = dot(r0, r0);
	r0.x = 1 / sqrt(r0.x);
	r0.x = 1 / r0.x;
	o.w = r0.x * -5;

	return o;
}
