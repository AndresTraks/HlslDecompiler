vs_3_0
def c20, 8, -4, 0, 15
dcl_position v0
dcl_texcoord v1
dcl_position o0
dcl_color o1
dp4 o0.x, v0, c0
dp4 o0.y, v0, c1
dp4 o0.z, v0, c2
dp4 o0.w, v0, c3
mul r0.x, c20.x, v1.x
frc r0.x, r0.x
slt r0.y, -r0.x, r0.x
mad r0.z, v1.x, c20.x, c20.y
add r0.x, r0.z, -r0.x
slt r0.z, r0.z, -r0.z
mad r0.x, r0.z, r0.y, r0.x
max r0.x, r0.x, c20.z
min r0.x, r0.x, c20.w
mova a0.x, r0.x
mov o1, c4[a0.x]
