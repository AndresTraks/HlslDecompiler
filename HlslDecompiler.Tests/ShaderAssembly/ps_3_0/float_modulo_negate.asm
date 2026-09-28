ps_3_0
dcl_texcoord v0
rcp r0.x, -c0.z
mul r0, r0.x, v0
frc r1, r0_abs
cmp r0, r0, r1, -r1
mad oC0, r0, c0.z, c0.w
