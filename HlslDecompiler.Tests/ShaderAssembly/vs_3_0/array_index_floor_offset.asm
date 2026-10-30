vs_3_0
def c56, 2, 4, 1, 0
dcl_position v0
dcl_blendindices v1
dcl_position o0
add r0.x, v1.y, v1.y
frc r0.x, r0.x
mad r0.y, v1.y, c56.x, c56.z
add r0.x, r0.y, -r0.x
mova a0.x, r0.x
add r0.x, c56.x, v1.x
frc r0.y, v1.x
add r0.x, r0.x, -r0.y
mul r0.x, r0.x, c56.y
mova a0.y, r0.x
dp4 r0.x, v0, c0[a0.y]
dp4 r0.y, v0, c1[a0.y]
dp4 r0.z, v0, c2[a0.y]
dp4 r0.w, v0, c3[a0.y]
add o0, r0, c40[a0.x]
