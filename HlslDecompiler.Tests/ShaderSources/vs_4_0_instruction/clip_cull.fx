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

	o.sv_position.x = dot(position, transpose(wvp)[0]);
	o.sv_position.y = dot(position, transpose(wvp)[1]);
	o.sv_position.z = dot(position, transpose(wvp)[2]);
	o.sv_position.w = dot(position, transpose(wvp)[3]);
	o.sv_clipdistance = dot(position, planes[0]);
	o.sv_culldistance = dot(position, planes[1]);

	return o;
}
