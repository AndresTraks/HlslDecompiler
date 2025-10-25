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
	r0 = i.position2 + float4(2, 2, 2, 2);
	r0.x = dot(r0, r0);
	r0.x = sqrt(r0.x);
	o.w = r0.x * -5;
	r0.x = dot(i.position2.xy, i.position2.xy);
	r0.x = sqrt(r0.x);
	o.z = -(r0.x);
	r0.x = dot(i.position, i.position);
	o.x = sqrt(r0.x);
	r0.x = dot(i.position1.xyz, i.position1.xyz);
	o.y = sqrt(r0.x);

	return o;
}
