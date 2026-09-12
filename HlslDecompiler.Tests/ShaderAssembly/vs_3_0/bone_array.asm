vs_3_0
dcl_blendindices v0
dcl_blendweight v1
dcl_position o0
frc r0.xyz, v0.xyz
add r0.xyz, -r0.xyz, v0.xyz
mova a0.x, r0.y
mul r1, v1.y, c0[a0.x]
mova a0.xy, r0.xz
mad r0, c0[a0.x], v1.x, r1
mad r0, c0[a0.y], v1.z, r0
dp4 o0.x, r0, c16
dp4 o0.y, r0, c17
dp4 o0.z, r0, c18
dp4 o0.w, r0, c19
