ps_3_0
def c1, 4, -2, 1, 0
def c2, 0.5, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
texld r0, v0.xy, s0
mad_pp r0.xyz, r0.xyz, c1.xxx, c1.yyy
add r1.xyz, -r0.xyz, c1.zww
mad_pp oC0.xyz, c0.xxx, r1.xyz, r0.xyz
mov oC0.w, c2.x
