using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
  [Info("FileIndexer", "ImagicTheCat", "1.0.0")]

  public class FileIndexer : CovalencePlugin{
    [Flags]
    public enum Search{
      File = 1,
      Directory = 2,
      Recursive = 4
    }
    
    public class Index{
      public Dictionary<string, Index> idxs;

      public Index()
      {
        idxs = new Dictionary<string, Index>();
      }

      //index a path 
      public void IndexFile(List<string> path, int shift = 0)
      {
        if(path != null && shift < path.Count){
          Index idx;
          if(!idxs.TryGetValue(path[shift],out idx)){ //get existing index or create it
            idx = new Index();
            idxs.Add(path[shift],idx);
          }

          idx.IndexFile(path,shift+1); //recursive index creation
        }
      }

      //get a child index
      public Index GetIndex(List<string> path, int shift = 0)
      {
        if(path != null){ //recursive search
          if(shift < path.Count){ 
            Index idx;
            if(idxs.TryGetValue(path[shift],out idx)) //recursive search
              return idx.GetIndex(path, shift+1);
            else 
              return null;
          }
          else
            return this;
        }
        else
          return null;
      }

      //check if this index is a file (no children)
      public bool IsFile()
      {
        return (idxs.Count == 0);
      }

      //list directories/files in this index (fullpaths)
      public List<string> List(Search search)
      {
        List<string> paths = new List<string>();
        _List(search, paths, "");

        return paths;
      }

      private void _List(Search search, List<string> outpaths, string path)
      {
        foreach(var e in idxs){
          if(e.Value.IsFile()){ //file
            if((search & Search.File) != 0) outpaths.Add(path+e.Key);
          }
          else{ //directory
            if((search & Search.Directory) != 0) outpaths.Add(path+e.Key+"/");

            if((search & Search.Recursive) != 0)
              e.Value._List(search, outpaths, path+e.Key+"/");
          }
        }
      }

      public void Cleanup(string path)
      {
        if(path != null){
          List<string> toremove = new List<string>();

          foreach(var e in idxs){
            if(e.Value.IsFile()){ //file
              if(!Interface.Oxide.DataFileSystem.ExistsDatafile(path+e.Key)) //file don't exist, remove entry
                toremove.Add(e.Key);
            }
            else{ //directory
              e.Value.Cleanup(path+e.Key+"/");             
              if(e.Value.IsFile()) //directory cleaned-up his own content, delete it
                toremove.Add(e.Key);
            }
          }

          foreach(var e in toremove)
            idxs.Remove(e);
        }
      }
    }   

    public class Indexer{
      public Index root;
      private string filepath;

      public Indexer(string filepath)
      {
        this.filepath = filepath;
        Load();
      }

      //index a file (autosave if save not specified)
      public void IndexFile(string filepath, bool save = true)
      {
        bool is_file;
        List<string> list = ParsePath(filepath, out is_file);

        if(is_file){
          root.IndexFile(list);
          if(save)
            Save();
        }
      }

      //list files/directories
      public List<string> List(string path = "", Search search = Search.File | Search.Recursive)
      {
        if(path != null){
          //find index by path
          string[] patharray = path.Split(new string[]{"/"}, StringSplitOptions.RemoveEmptyEntries);
          Index idx = root.GetIndex(new List<string>(patharray)); 

          //list indexes from this index
          if(idx != null)
            return idx.List(search);
        }

        return new List<string>();
      }

      public void Load()
      {
        root = Interface.Oxide.DataFileSystem.ReadObject<Index>(filepath);
        if(root == null)
          root = new Index();
      }

      public void Save()
      {
        Interface.Oxide.DataFileSystem.WriteObject(filepath, root);
      }

      //perform a cleanup of the indexes, checking if a file exists in the root directory
      //rootpath must be like this: my_directory/other/, not my_directory/other
      //don't save the indexer, you must use Save() yourself
      //IndexFile with default parameters will also save the changes.
      public void Cleanup(string rootpath)
      {
        if(rootpath != null)
          root.Cleanup(rootpath);
      }

      public static List<string> ParsePath(string path, out bool is_file)
      {
        List<string> list = new List<string>();
        is_file = false;

        if(path != null){
          string[] res = path.Split(new string[]{"/"}, StringSplitOptions.None);
          for(int i = 0; i < res.Length; i++){
            if(res[i].Length > 0){
              list.Add(res[i]);
              if(i == res.Length-1)
                is_file = true;
            }
          }
        }

        return list;
      }
    }
  }
}