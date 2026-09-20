float4x4 wvp;
float4 planes[2];

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float sv_clipdistance : SV_ClipDistance;
	float sv_culldistance : SV_CullDistance;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.sv_position = mul(position, wvp);
	o.sv_clipdistance = dot(planes[0], position);
	o.sv_culldistance = dot(planes[1], position);

	return o;
}
