ps_3_0
dcl_texcoord v0
rcp r0.x, -c0.z
mul r0, r0.x, v0.zwxy
frc r1, r0_abs
cmp r0, r0, r1, -r1
mul r0, r0, -c0.z
rcp r1.x, c0.y
mul r1, r1.x, v0
frc r2, r1_abs
cmp r1, r1, r2, -r2
mad r0, r1, c0.y, r0
rcp r1.x, c0.x
mul r1, r1.x, v0
frc r2, r1_abs
cmp r1, r1, r2, -r2
mad r1, r1, -c0.x, c0.w
add oC0, r0, r1
