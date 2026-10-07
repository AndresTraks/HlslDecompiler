ps_1_4
def c0, 0.0799999982, -0.0599999987, 0, 0
def c1, 0.899999976, 0.800000012, 0.699999988, 1
texld r0, t0
texcrd r1.xyz, t1.xyz
mad r1.xy, r0_bx2.xy, c0.xy, r1.xy
phase
texld r1, r1
texld r2, t2_dw.xyww
mul r0.xyz, r1.xyz, v0.xyz
+ mul r0.w, r2_x2.w, v0.w
lrp r0.xyz, r2.xyz, r0.xyz, c1.xyz
cnd r0.xyz, r2.xyz, r0.xyz, 1-r1.xyz
