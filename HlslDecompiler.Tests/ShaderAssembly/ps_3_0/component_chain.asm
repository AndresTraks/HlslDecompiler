ps_3_0
def c1, 2, 1, 0, 0
mov r0.xyz, c0.xyw
rep i0
mad r0.y, r0.y, c1.x, c1.y
add r0.x, r0.y, c0.z
add r0.z, r0.x, -c0.w
endrep
mov oC0.xyw, r0.xyz
mov oC0.z, c0.z
