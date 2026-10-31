xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
dcl_texcoord0 r0
dcl_color1_centroid r1
/*    0.0 */       alloc colors
/*    0.1 */       exece
/*    1   */          add oC0, r1.zyxw, r0.wwww
