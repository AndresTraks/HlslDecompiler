ps_1_1
def c0, 0.200000003, 0.200000003, 0.25, 1
tex t0
tex t1
dp3_sat r1, t0_bx2.xyz, v0_bx2.xyz
add r1, r1, c0
mul r0.xyz, t1.xyz, r1.xyz
+ mov r0.w, 1-t1.w
