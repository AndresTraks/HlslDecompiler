ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 4
add r0.x, -cb0[0].x, cb0[0].y
div r0.x, l(1, 1, 1, 1), r0.x
add r1, v0, -cb0[0].x
mul_sat r0, r0.x, r1
mad r1, r0, l(-2, -2, -2, -2), l(3, 3, 3, 3)
mul r0, r0, r0
max r2, v0, cb0[0].z
min r2, r2, cb0[0].w
mad r0, r1, r0, r2
add r1, v0, -cb0[0]
lt r2, l(0, 0, 0, 0), r1
lt r3, r1, l(0, 0, 0, 0)
mad r1, cb0[0].w, r1, cb0[0]
iadd r2, -r2, r3
itof r2, r2
add r0, r0, r2
div r2, v0, -cb0[0].x
ge r3, r2, -r2
frc r2, |r2|
movc r2, r3, r2, -r2
mad r0, r2, -cb0[0].x, r0
ge r2, v0, cb0[0].y
and r2, r2, l(1065353216, 1065353216, 1065353216, 1065353216)
add r0, r0, r2
add o0, r1, r0
ret
