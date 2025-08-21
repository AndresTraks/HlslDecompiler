ps_3_0
dcl_texcoord v0
dcl_2d s0
dcl_2d s1
dcl_2d s2
dcl_2d s5
texld oC0, v0.xy, s0
texldd oC1, v0.xy, s2, v0.xy, v0.yx
texldl oC2, v0, s1
texldp oC3, v0.xyyw, s5
