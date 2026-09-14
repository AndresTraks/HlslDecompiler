vs_1_1
dcl_position v0
dcl_color v1
dcl_texcoord v2
dp4 oPos.x, v0, c0
dp4 oPos.y, v0, c1
dp4 oPos.z, v0, c2
dp4 oPos.w, v0, c3
slt r0.x, v2.x, -v2.x
frc r0.y, v2.x
add r0.z, -r0.y, v2.x
slt r0.y, -r0.y, r0.y
mad r0.x, r0.x, r0.y, r0.z
mov a0.x, r0.x
mul r0, v1, c4[a0.x]
slt r1, v1, c8
frc r3.xy, v1.zw
mov r2.zw, r3.xy
frc r2.xy, v1.xy
mul r1, r1, r2
sge r2, v1, c8
mad r0, r2, r0, r1
exp r1.x, v1.x
add r0, r0, r1.x
log r1.x, v1.y
add oD0, r0, r1.x
