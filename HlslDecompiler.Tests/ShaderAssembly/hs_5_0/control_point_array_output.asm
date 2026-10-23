hs_5_0
hs_decls
dcl_input_control_point_count 3
dcl_output_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_tessellator_partitioning partitioning_integer
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
hs_control_point_phase
dcl_input vOutputControlPointID
dcl_input vPrim
dcl_input v[3][0].xyz
dcl_input v[3][1].xy
dcl_output o0.xyz
dcl_output o1.x
dcl_output o2.x
dcl_output o3.x
dcl_output o4.x
dcl_temps 1
mov r0.x, vOutputControlPointID
mov o0.xyz, v[r0.x][0].xyz
dp2 o1.x, v[r0.x][1].xy, v[0][1].xy
dp2 r0.y, v[r0.x][1].xy, v[1][1].xy
dp2 r0.x, v[r0.x][1].xy, v[2][1].xy
add o3.x, r0.x, l(2)
add o2.x, r0.y, l(1)
imad o4.x, vPrim, l(3), vOutputControlPointID
ret
hs_fork_phase
dcl_output_siv o3.x, finalTriInsideTessFactor
mov o3.x, l(2)
ret
hs_join_phase
dcl_input vPrim
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
dcl_temps 1
and r0.x, vPrim, l(3)
iadd r0.x, r0.x, l(2)
utof r0.x, r0.x
mov o0.x, r0.x
mov o1.x, r0.x
mov o2.x, r0.x
ret
