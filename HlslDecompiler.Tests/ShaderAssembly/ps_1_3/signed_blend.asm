ps_1_3
def c0, 0.5, 0.25, 0.75, 0.600000024
def c1, 0.300000012, -0.699999988, 0.400000006, 0.899999976
tex t0
texcoord t1
texkill t2
sub_d2 r0, t0, t1_bias
mad_x4_sat r1, r0, -t1_bx2, v1
lrp r0.xyz, c0.xyz, r1.xyz, t0.xyz
+ add r0.w, r1.w, r0.z
dp4_d4 r1, r0, c1
cmp r0.xyz, r1_bias.xyz, r0.xyz, -t0_bias.xyz
cnd r0, r0.w, r0, 1-t1
