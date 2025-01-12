ps_3_0
dcl_texcoord v0
dcl_2d s0
texld oC0, v0.xy, s0
texldd oC1, v0.xy, s0, v0.xy, v0.yx
texldl oC2, v0, s0
texldp oC3, v0.xyyw, s0
