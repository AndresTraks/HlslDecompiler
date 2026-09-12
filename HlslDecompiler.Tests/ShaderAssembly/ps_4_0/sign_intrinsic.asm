ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 3
add r0.x, -cb0[0].x, cb0[0].y
div r0.x, l(1, 1, 1, 1), r0.x
add r1, v0, -cb0[0].x
mul_sat r0, r0.x, r1
mad r1, r0, l(-2, -2, -2, -2), l(3, 3, 3, 3)
mul r0, r0, r0
max r2, v0, -cb0[0].z
min r2, r2, cb0[0].z
mad r0, r1, r0, r2
div r1, v0, cb0[0].w
ge r2, r1, -r1
frc r1, |r1|
movc r1, r2, r1, -r1
mad r0, r1, cb0[0].w, r0
lt r1, l(0, 0, 0, 0), v0
lt r2, v0, l(0, 0, 0, 0)
iadd r1, -r1, r2
itof r1, r1
add r2.xy, v0.xy, -cb0[0].xy
dp2 r2.x, r2.xy, r2.xy
sqrt r2.x, r2.x
mad o0, r1, r2.x, r0
ret
