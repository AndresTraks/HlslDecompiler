xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=7
dcl_texcoord0 r0
dcl_texcoord1 r1
dcl_texcoord2 r2
dcl_texcoord3 r3
dcl_texcoord4 r4
dcl_texcoord5 r5
dcl_texcoord6 r6
dcl_color1_centroid r7
/*    0.0 */       alloc colors
/*    0.1 */       exece
/*    1   */          mul r6, r6, r7
/*    2   */          mad r4, r4, r5, r6
/*    3   */          mad r2, r2.xzwy, r3.xzwy, r4.xzwy
/*    4   */          mad oC0, r0, r1, r2.xwyz
