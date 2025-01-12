ps_3_0
def c0, 1, 0, 0, 0
dcl_texcoord v0
dcl_2d s0
texld r0, v0.xy, s0
texldb r1, v0, s0
add oC0, r0, r1
texldd oC1, v0.xx, s0, v0.xx, v0.yy
texldd oC2, c0.xx, s0, v0.xx, v0.xx
texldl r0, v0, s0
texldp r1, v0.xyyw, s0
add oC3, r0, r1
