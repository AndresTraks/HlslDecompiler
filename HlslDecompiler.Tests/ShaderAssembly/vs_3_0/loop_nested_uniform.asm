vs_3_0
def c5, 0, 2, 3, 0
dcl_position o0
mov r0.x, c5.x
slt r0.x, r0.x, c4.x
slt r0.y, r0.x, c4.x
lrp r1.x, r0.y, c5.y, r0.x
slt r0.z, r1.x, c4.x
mul r0.z, r0.z, r0.y
lrp r2.x, r0.z, c5.z, r1.x
slt r0.w, r2.x, c4.x
mul r0.w, r0.w, r0.z
mov r1, c5.x
rep i0
mad r2, r0.x, c0, r1
mad r2, r0.y, c1, r2
mad r2, r0.z, c2, r2
mad r1, r0.w, c3, r2
endrep
mov o0, r1
